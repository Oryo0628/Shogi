using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class GameSceneDirector : MonoBehaviour
{
    // UI関連
    [SerializeField] Text textTurnInfo;
    [SerializeField] Text textResultInfo;
    [SerializeField] Button titleButton;
    [SerializeField] Button rematchButton;
    [SerializeField] Button evolutionApplyButton;
    [SerializeField] Button evolutionCancelButton;

    // ゲーム設定
    const int playerMax = 2;
    int boardWidth;
    int boardHeight;

    // タイルのPrefab
    [SerializeField] GameObject prefabTile; 

    // ユニットのPrefab
    [SerializeField] List<GameObject> prefabUnits;

    // 初期配置
    int[ , ] boardSetting = 
    {
        {4, 0, 1, 0, 0, 0, 11, 0, 14},
        {5, 2, 1, 0, 0, 0, 11,13, 15},
        {6, 0, 1, 0, 0, 0, 11, 0, 16},
        {7, 0, 1, 0, 0, 0, 11, 0, 17},
        {8, 0, 1, 0, 0, 0, 11, 0, 18},
        {7, 0, 1, 0, 0, 0, 11, 0, 17},
        {6, 0, 1, 0, 0, 0, 11, 0, 16},
        {5, 3, 1, 0, 0, 0, 11,12, 15},
        {4, 0, 1, 0, 0, 0, 11, 0, 14},
    };

    // フィールドデータ
    Dictionary<Vector2Int, GameObject> tiles;
    UnitController[,] units;
    
    // 現在選択中のユニット
    UnitController selectUnit;

    // 移動可能な範囲
    Dictionary<GameObject, Vector2Int> movableTiles;

    // カーソルのPrefab
    [SerializeField] GameObject prefabCursor;
    // カーソルオブジェクト
    List<GameObject> cursors;

    // プレイヤーとターン
    int nowPlayer;
    int turnCount;
    bool isCpu;

    // mode
    enum Mode
    {
        None,
        Start,
        Select,
        WaitEvolution,
        TurnChange,
        Result
    }

    Mode nowMode, nextMode;

    // 持ち駒タイルのPrefab
    [SerializeField] GameObject prefabUnitTile;

    // 持ち駒を置く場所
    List<GameObject>[] unitTiles;

    // キャプチャされたユニット
    List<UnitController> caputuredUnits;

    // Start is called before the first frame update
    void Start()
    {
        // UI関連の初期設定
        titleButton.gameObject.SetActive(false);
        rematchButton.gameObject.SetActive(false);
        evolutionApplyButton.gameObject.SetActive(false);
        evolutionCancelButton.gameObject.SetActive(false);
        textResultInfo.text = "";

        // ボードサイズ
        boardWidth = boardSetting.GetLength(0);
        boardHeight = boardSetting.GetLength(1);

        // フィールドの初期化
        tiles = new Dictionary<Vector2Int, GameObject>();
        units = new UnitController[boardWidth, boardHeight];

        // 移動可能な範囲の初期化
        movableTiles = new Dictionary<GameObject, Vector2Int>();
        cursors = new List<GameObject>();

        // 持ち駒を置く場所
        unitTiles = new List<GameObject>[playerMax];

        // キャプチャされたユニットの初期化
        caputuredUnits = new List<UnitController>();

        // タイルの生成
        for (int i = 0; i < boardWidth; i++)
        {
            for (int j = 0; j < boardHeight; j++)
            {
                // タイルとユニットのポジション
                float x = i - boardWidth / 2;
                float z = j - boardHeight / 2;

                Vector3 pos = new Vector3(x, 0, z);

                // タイルのインデックス
                Vector2Int tileindex = new Vector2Int(i, j);

                // タイルの生成
                GameObject tile = Instantiate(prefabTile, pos, Quaternion.identity);
                tiles.Add(tileindex, tile);



                // ユニットの生成
                int type = boardSetting[i,j] % 10;
                int player = boardSetting[i, j] / 10;

                if (type == 0) continue;

                // 初期化
                pos.y = 0.7f;

                GameObject prefab = prefabUnits[type - 1];
                GameObject unit = Instantiate(prefab, pos, Quaternion.Euler(90, player * 180, 0));
                unit.AddComponent<Rigidbody>(); 

                UnitController unitctrl = unit.AddComponent<UnitController>();
                unitctrl.Init(player, type, tile, tileindex);

                // ユニットデータのセット
                units[i,j] = unitctrl;
            }
        }

        // 持ち駒を置く場所作成
        Vector3 startpos = new Vector3(5, 0.5f, -2);

        for (int i = 0; i < playerMax; i++)
        {
            unitTiles[i] = new List<GameObject>();
            int dir = (0 == i) ? 1 : -1;

            for (int j = 0; j < 9; j++)
            {
                Vector3 pos = startpos;
                pos.x = (pos.x + j % 3) * dir;
                pos.z = (pos.z - j % 3) * dir;

                GameObject obj = Instantiate(prefabUnitTile, pos, Quaternion.identity);
                unitTiles[i].Add(obj);

                obj.SetActive(false);
            }
        }

        // TurnChangeから始める場合-1
        nowPlayer = -1;

        // 初期モード
        nowMode = Mode.None;
        nextMode = Mode.TurnChange;

    }


    // Update is called once per frame
    void Update()
    {
        if (nowMode == Mode.Start)
        {
            StartMode();
        }
        else if (nowMode == Mode.Select)
        {
            SelectMode();
        }
        else if (nowMode == Mode.TurnChange)
        {
            TurnChangeMode();
        }

        // モードの変更
        if (nextMode != Mode.None)
        {
            nowMode = nextMode;
            nextMode = Mode.None;
        }
    }

    // 選択時
    void setSelectCursors(UnitController unit = null, bool playerunit = true)
    {

        // カーソル削除
        foreach (var item in cursors)
        {
            Destroy(item);
        }
        cursors.Clear();
        

        // 選択ユニットの非選択状態に戻す
        if (selectUnit)
        {
            selectUnit.Select(false);
            selectUnit = null;
        }

        if (!unit) return ;

        // 移動可能範囲の取得
        List<Vector2Int> movabletiles = getMovableTiles(unit);
        movableTiles.Clear(); 

        foreach (var item in movabletiles)
        {
            movableTiles.Add(tiles[item], item);

            // カーソルの生成
            Vector3 pos = tiles[item].transform.position;
            pos.y += 0.51f;
            GameObject cursor = Instantiate(prefabCursor, pos, Quaternion.identity);
            cursors.Add(cursor);
        }

        // 選択状態
        if (playerunit)
        {
            unit.Select();
            selectUnit = unit;
        }

    }

    // ユニットの移動
    Mode moveUnit(UnitController unit, Vector2Int tileindex)
    {
        // 移動し終わった後のモード
        Mode ret = Mode.TurnChange;
        
        // 現在位置
        Vector2Int oldpos = unit.pos;

        // 移動先に誰かがいたらとる
        CaptureUnit(nowPlayer, tileindex);

        // ユニットの移動
        unit.Move(tiles[tileindex], tileindex);

        // 内部データの更新（新しい場所）
        units[tileindex.x, tileindex.y] = unit;

        // ボード上の駒を更新
        if (unit.fieldStatus == FieldStatus.OnBoard)
        {
            // 内部データの更新
            units[oldpos.x, oldpos.y] = null;
        }
        else
        {
            // 持ち駒の更新
            caputuredUnits.Remove(unit);
        }

        // ユニットの状態を更新
        unit.fieldStatus = FieldStatus.OnBoard;

        // 持ち駒表示を更新
        AlignCaputureUnits(nowPlayer);

        return ret;
    }

    // 移動可能範囲の取得
    List<Vector2Int> getMovableTiles(UnitController unit)
    {
        List<Vector2Int> ret = unit.GetMovableTiles(units);

        // TODO 王手されるかどうか

        return ret;
    }

    // ターン開始
    void StartMode()
    {
        // 勝敗がついていなければ通常モード
        nextMode = Mode.Select;

        // Infoの更新
        textTurnInfo.text = "" + (nowPlayer + 1) + "Pの番";
        textResultInfo.text = "";
    }

    // ユニットとタイル選択
    void SelectMode()
    {
        GameObject tile = null;
        UnitController unit = null;
        
        // プレイヤーの処理
        if ( Input.GetMouseButtonUp(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            // 手前のユニットにも当たり判定があるため，ヒットしたすべてのオブジェクト情報を取得
            foreach (RaycastHit hit in Physics.RaycastAll(ray))
            {
                UnitController hitUnit = hit.transform.GetComponent<UnitController>();

                // 持ち駒
                if (hitUnit && FieldStatus.Captured == hitUnit.fieldStatus)
                {
                    unit = hitUnit;
                }

                // タイルから駒を選択
                else if (tiles.ContainsValue(hit.transform.gameObject))
                {
                    tile = hit.transform.gameObject;
                    // タイルからユニットを探す
                    foreach (var item in tiles)
                    {
                        if (item.Value == tile)
                        {
                            unit = units[item.Key.x, item.Key.y];
                        }
                    }
                    break;
                }
            }
        }

        // 何も選択されていなければ処理をしない
        if (tile == null && unit == null) return;

        // 移動先の選択
        if (tile && selectUnit && movableTiles.ContainsKey(tile))
        {
            nextMode = moveUnit(selectUnit, movableTiles[tile]);
        }

        // ユニット選択
        if (unit)
        {
            bool isplayer = nowPlayer == unit.player;
            setSelectCursors(unit, isplayer);
        }
    }

    // ターン変更
    void TurnChangeMode()
    {
        // ボタンとカーソルのリセット
        setSelectCursors();
        evolutionApplyButton.gameObject.SetActive(false);
        evolutionCancelButton.gameObject.SetActive(false);

        // CPUの状態解除
        isCpu = false;

        // 次のプレイヤーへ
        nowPlayer = GetNextPlayer(nowPlayer);

        // 経過ターン
        if (nowPlayer == 0)
        {
            turnCount++;
        }

        nextMode = Mode.Start;
    }

    // 次のプレイヤー番号を返す
    public static int GetNextPlayer(int player)
    {
        int next = player + 1;
        if (playerMax <= next)
        {
            next = 0;   
        }
        return next;
    }

    // ユニットを持ち駒にする
    void CaptureUnit(int player, Vector2Int tileindex) // tileindex : 移動先のインデックス
    {
        UnitController unit = units[tileindex.x, tileindex.y];
        if (!unit) return;
        unit.Capture(player);
        caputuredUnits.Add(unit);
        units[tileindex.x, tileindex.y] = null;
    }

    // 持ち駒を並べる
    void AlignCaputureUnits(int player)
    {
        // 所持個数を非表示
        foreach (var item in unitTiles[player])
        {
            item.SetActive(false);
        }

        // ユニットごとに分ける
        Dictionary<UnitType, List<UnitController>> typeunits = new Dictionary<UnitType, List<UnitController>>();

        foreach (var item in caputuredUnits)
        {
            if (player != item.player) continue;
            typeunits.TryAdd(item.unitType, new List<UnitController>());
            typeunits[item.unitType].Add(item);
        }

        // タイプごとに並べて一番上だけ表示
        int tilecount = 0;
        foreach (var item in typeunits)
        {
            if (item.Value.Count < 1) continue;

            // 置く場所
            GameObject tile = unitTiles[player][tilecount++];

            // 非表示にしていたタイルを表示
            tile.SetActive(true);

            // 所持個数の表示
            tile.transform.GetChild(0).gameObject.GetComponent<TextMeshPro>().text = "" + item.Value.Count;

            //  同じ種類の持ち駒を並べる
            for (int i = 0; i < item.Value.Count; i++)
            {
                // リスト内のユニットを表示
                GameObject unit = item.Value[i].gameObject;

                // 置く場所
                Vector3 pos = tile.transform.position;
                // ユニットを移動して表示
                unit.SetActive(true);
                unit.transform.position = pos;
                // 1個目以外は非表示
                if (0 < i) unit.SetActive(false);
            }

        }
    }

    // 指定された配列をコピーして返す
    public static UnitController[,] GetCopyArray(UnitController[,] ary)
    {
        UnitController[,] ret = new UnitController[ary.GetLength(0), ary.GetLength(1)];
        Array.Copy(ary, ret, ary.Length);
        return ret;
    }

    // 指定された配置で王手しているユニットを返す
    public static List<UnitController> GetOuteUnit(UnitController[,] units, int player, bool checkotherunit = true)
    {
        List<UnitController> ret = new List<UnitController>();

        foreach (var unit in units)
        {
            // 仲間のユニットだったら
            if (!unit || player == unit.player) continue;

            // ユニットの移動可能範囲
            List<Vector2Int> movabletiles = unit.GetMovableTiles(units, checkotherunit);

            foreach (var tile in movabletiles)
            {
                // ユニットがいなければ
                if (!units[tile.x, tile.y]) continue;

                if (units[tile.x, tile.y].unitType == UnitType.Gyoku)
                {
                    ret.Add(unit);
                }
            }

        }
        
        return ret;

    }
}
