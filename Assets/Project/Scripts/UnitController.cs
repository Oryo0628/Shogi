using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

// 駒のタイプ
public enum UnitType{
    None = -1,
    Hu = 1,
    Kaku,
    Hisha,
    Kyousha,
    Keima,
    Gin,
    Kin,
    Gyoku,
    // 成
    Tokin,
    Uma,
    Ryu,
    Narikyo,
    Narikei,
    NariGin
}

// 駒の場所
public enum FieldStatus
{
    OnBoard,
    Captured,
}


public class UnitController : MonoBehaviour
{
    // ユニットのプレイヤー番号
    public int player;
    // ユニットの種類
    public UnitType unitType, oldUnitType;
    // ユニットの場所
    public FieldStatus fieldStatus;

    // 成テーブル
    Dictionary<UnitType, UnitType> evolutionTable = new Dictionary<UnitType, UnitType>()
    {
        { UnitType.Hu, UnitType.Tokin},
        { UnitType.Kaku, UnitType.Uma},
        { UnitType.Hisha, UnitType.Ryu},
        { UnitType.Kyousha, UnitType.Narikyo},
        { UnitType.Keima, UnitType.Narikei},
        { UnitType.Gin, UnitType.NariGin},
        { UnitType.Kin, UnitType.None},
        { UnitType.Gyoku, UnitType.None},

    };
    
    // ユニット選択/非選択のy座標
    public const float SelectUnitY = 1.5f;
    public const float UnSelectUnitY = 0.7f;

    // 置いている場所
    public Vector2Int pos;

    // 選択される前のy座標
    float oldPosY;

    // 初期化
    public void Init(int player, int unitType, GameObject tile, Vector2Int pos)
    {
        this.player = player;
        this.unitType = (UnitType)unitType;
        // 取られたときに元に戻るように
        this.oldUnitType = (UnitType)unitType;
        // 場所の初期値
        this.fieldStatus = FieldStatus.OnBoard;
        // 角度と場所
        transform.eulerAngles = getDefaultAngles(player);
        Move(tile, pos);
    }

    // 指定されたプレイヤー番号の向きを返す 
    Vector3 getDefaultAngles(int player)
    {
        return new Vector3(90, player * 180, 0);
    }

    // 移動処理
    public void Move(GameObject tile, Vector2Int tileindex)
    {
        Vector3 pos = tile.transform.position;
        pos.y = UnSelectUnitY;
        transform.position = pos;
        this.pos = tileindex;
    }

    // ユニット選択時の処理
    public void Select(bool select = true)
    {
        Vector3 pos = transform.position;
        bool iskinematic = select;

        if (select)
        {
            oldPosY = pos.y;
            pos.y = SelectUnitY;
        }
        else
        {
            pos.y = UnSelectUnitY;

            // 持ち駒の位置は特別
            if (FieldStatus.Captured == fieldStatus)
            {
                pos.y = oldPosY;
                iskinematic = true;
            }
        }

        GetComponent<Rigidbody>().isKinematic = iskinematic;
        transform.position = pos;
    }

    // 移動可能範囲の取得
    public List<Vector2Int> GetMovableTiles(UnitController[,] units, bool checkotherunit = true)
    {
        List<Vector2Int> ret = new List<Vector2Int>();

        // 持ち駒の状態
        if (fieldStatus == FieldStatus.Captured)
        {
            foreach (var checkpos in getEmptyTiles(units))
            {
                // 移動可能
                bool ismovable = true;

                // 移動した状態を作る
                pos = checkpos;
                fieldStatus = FieldStatus.OnBoard;

                // 置いたあと移動できないなら移動不可
                if (1 > getMovableTiles(units, unitType).Count)
                {
                    ismovable = false;
                }

                // 歩
                if (unitType == UnitType.Hu)
                {
                    // 二歩
                    for (int i = 0; i < units.GetLength(1); i++)
                    {
                        var otherunit = units[checkpos.x, i];
                        if (otherunit && otherunit.unitType == UnitType.Hu && otherunit.player == this.player)
                        {
                            ismovable = false;
                            break;
                        }
                    }

                    // 打ち歩詰め
                    int nextplayer = GameSceneDirector.GetNextPlayer(player);

                    // 撃ったことにして王手になる場合
                    UnitController[,] copyunits = GameSceneDirector.GetCopyArray(units);
                    copyunits[checkpos.x, checkpos.y] = this;
                    int outecount = GameSceneDirector.GetOuteUnit(copyunits, nextplayer, false).Count;

                    if (0 < outecount && ismovable)
                    {
                        ismovable = false;
                        //相手のいずれかの駒が歩をとった状態を再現
                        foreach(var unit in units)
                        {
                            if (!unit || nextplayer != unit.player) continue;
                            // 移動範囲にchechposがない場合
                            if (!unit.GetMovableTiles(copyunits).Contains(checkpos)) continue;
                            // 相手の駒を移動させた状態を作る
                            copyunits[checkpos.x, checkpos.y] = unit;
                            outecount = GameSceneDirector.GetOuteUnit(copyunits, nextplayer, false).Count;
                            // 1つでも王手を回避できる駒があれば打ち歩詰めではない
                            if (1 > outecount)
                            {
                                ismovable = true;
                            }
                        }
                    }
                }

                // 移動不可
                if (!ismovable) continue;

                ret.Add(checkpos);
            }

            // 移動状態をもとに戻す
            pos = new Vector2Int(-1, -1);
            fieldStatus = FieldStatus.Captured;
        }
        // 玉
        else if (unitType == UnitType.Gyoku)
        {
            ret = getMovableTiles(units, UnitType.Gyoku);

            // 相手の移動範囲を考慮しない
            if (!checkotherunit) return ret;

            // 削除される可能性のあるタイル
            List<Vector2Int> removetiles = new List<Vector2Int>();

            foreach (var item in ret)
            {
                // 移動した状態を作っておいてされているなら削除対象
                UnitController[,] copyunits = GameSceneDirector.GetCopyArray(units);

                // 今いる場所から移動した状態にする
                copyunits[pos.x, pos.y] = null;
                copyunits[item.x, item.y] = this;

                // 王手しているユニットの数
                int outeCount = GameSceneDirector.GetOuteUnit(copyunits, player, false).Count;
                if (0 < outeCount) removetiles.Add(item);
            }

            // 上記で取得したタイルを除外
            foreach (var item in removetiles)
            {
                ret.Remove(item);
            }
        }

        // 成で金と同じ動き
        else if (unitType == UnitType.Tokin
            || unitType == UnitType.Narikyo
            || unitType == UnitType.Narikei
            || unitType == UnitType.NariGin)
        {
            ret = getMovableTiles(units, UnitType.Kin);
        }

        // 馬 (角＋玉)
        else if (unitType == UnitType.Uma)
        {
            ret = getMovableTiles(units, UnitType.Gyoku);
            foreach (var item in getMovableTiles(units, UnitType.Kaku))
            {
                if (!ret.Contains(item)) ret.Add(item);
            }
        }

        // 龍 (飛車＋玉)
        else if (unitType == UnitType.Ryu)
        {
            ret = getMovableTiles(units, UnitType.Gyoku);
            foreach (var item in getMovableTiles(units, UnitType.Hisha))
            {
                if (!ret.Contains(item)) ret.Add(item);
            }
        }

        else
        {
            ret = getMovableTiles(units, unitType);
        }

        return ret;
    }

    // もととなる移動可能範囲の取得
    List<Vector2Int> getMovableTiles(UnitController[,] units, UnitType unittype)
    {
        List<Vector2Int> ret = new List<Vector2Int>();

        // 歩
        if (unittype == UnitType.Hu) 
        {
            // 駒の向き
            int dir = (0 == player) ? 1 : -1;

            // 前方に1マス
            List<Vector2Int> vec = new List<Vector2Int>()
            {
                new Vector2Int(0, 1 * dir),
            };

            // 実際のフィールドを調べる
            foreach (var item in vec)
            {
                Vector2Int checkpos = pos + item;
                if (!isCheckable(units, checkpos) || isFriendlyUnit(units[checkpos.x, checkpos.y]))
                {
                    continue;
                }

                ret.Add(checkpos);
            }
        }

        // 桂馬
        else if (unittype == UnitType.Keima)
        {
            // 駒の向き
            int dir = (player == 0) ? 1 : -1;

            List<Vector2Int> vec = new List<Vector2Int>()
            {
                new Vector2Int(1, 2 * dir),
                new Vector2Int(-1, 2 * dir),
            };

             // 実際のフィールドを調べる
            foreach (var item in vec)
            {
                Vector2Int checkpos = pos + item;
                if (!isCheckable(units, checkpos) || isFriendlyUnit(units[checkpos.x, checkpos.y]))
                {
                    continue;
                }

                ret.Add(checkpos);
            }
        }

        // 銀
        else if (unittype == UnitType.Gin)
        {
            // 駒の向き
            int dir = (player == 0) ? 1 : -1;

            List<Vector2Int> vec = new List<Vector2Int>()
            {
                new Vector2Int(-1, -1 * dir),
                new Vector2Int(1, -1 * dir),
                new Vector2Int(1, 1 * dir),
                new Vector2Int(-1, 1 * dir),
                new Vector2Int(0, 1 * dir),
            };

             // 実際のフィールドを調べる
            foreach (var item in vec)
            {
                Vector2Int checkpos = pos + item;
                if (!isCheckable(units, checkpos) || isFriendlyUnit(units[checkpos.x, checkpos.y]))
                {
                    continue;
                }

                ret.Add(checkpos);
            }
        }

        // 金
        else if (unittype == UnitType.Kin)
        {
            // 駒の向き
            int dir = (player == 0) ? 1 : -1;

            List<Vector2Int> vec = new List<Vector2Int>()
            {
                new Vector2Int(0, -1 * dir),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(1, 1 * dir),
                new Vector2Int(-1, 1 * dir),
                new Vector2Int(0, 1 * dir),
            };

             // 実際のフィールドを調べる
            foreach (var item in vec)
            {
                Vector2Int checkpos = pos + item;
                if (!isCheckable(units, checkpos) || isFriendlyUnit(units[checkpos.x, checkpos.y]))
                {
                    continue;
                }

                ret.Add(checkpos);
            }
        }

        // 玉
        else if (unittype == UnitType.Gyoku)
        {
            // 駒の向き
            int dir = (player == 0) ? 1 : -1;

            List<Vector2Int> vec = new List<Vector2Int>()
            {
                new Vector2Int(0, -1 * dir),
                new Vector2Int(1, -1 * dir),
                new Vector2Int(-1, -1 * dir),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(1, 1 * dir),
                new Vector2Int(-1, 1 * dir),
                new Vector2Int(0, 1 * dir),
            };

             // 実際のフィールドを調べる
            foreach (var item in vec)
            {
                Vector2Int checkpos = pos + item;
                if (!isCheckable(units, checkpos) || isFriendlyUnit(units[checkpos.x, checkpos.y]))
                {
                    continue;
                }

                ret.Add(checkpos);
            }
        }

        // 角
        else if (unittype == UnitType.Kaku)
        {
            List<Vector2Int> vec = new List<Vector2Int>()
            {
                new Vector2Int(1, 1),
                new Vector2Int(1, -1),
                new Vector2Int(-1, 1),
                new Vector2Int(-1, -1),
            };

            foreach (var item in vec)
            {
                Vector2Int checkpos = pos + item;
                while(isCheckable(units, checkpos))
                {
                    // 他の駒があった場合
                    if (units[checkpos.x, checkpos.y])
                    {
                        // 相手のユニットの場所へは移動可能
                        if (player != units[checkpos.x, checkpos.y].player)
                        {
                            ret.Add(checkpos);
                        }
                        break;
                    }

                    ret.Add(checkpos);
                    checkpos += item;                }
            }
        }

        // 飛車
        else if (unittype == UnitType.Hisha)
        {
            List<Vector2Int> vec = new List<Vector2Int>()
            {
                new Vector2Int(1, 0),
                new Vector2Int(0, -1),
                new Vector2Int(0, 1),
                new Vector2Int(-1, 0),
            };

            foreach (var item in vec)
            {
                Vector2Int checkpos = pos + item;
                while(isCheckable(units, checkpos))
                {
                    // 他の駒があった場合
                    if (units[checkpos.x, checkpos.y])
                    {
                        // 相手のユニットの場所へは移動可能
                        if (player != units[checkpos.x, checkpos.y].player)
                        {
                            ret.Add(checkpos);
                        }
                        break;
                    }

                    ret.Add(checkpos);
                    checkpos += item;                }
            }
        }

        // 香車
        else if (unittype == UnitType.Kyousha)
        {
            int dir = (0 == player) ? 1 : -1;
            List<Vector2Int> vec = new List<Vector2Int>()
            {
                new Vector2Int(0, 1 * dir),
            };

            foreach (var item in vec)
            {
                Vector2Int checkpos = pos + item;
                while(isCheckable(units, checkpos))
                {
                    // 他の駒があった場合
                    if (units[checkpos.x, checkpos.y])
                    {
                        // 相手のユニットの場所へは移動可能
                        if (player != units[checkpos.x, checkpos.y].player)
                        {
                            ret.Add(checkpos);
                        }
                        break;
                    }

                    ret.Add(checkpos);
                    checkpos += item;                }
            }
        }

        return ret;
    }

    // 配列オーバーかどうか
    bool isCheckable(UnitController[,] ary, Vector2Int idx)
    {
        // 配列オーバーの状態
        if (idx.x  < 0 || ary.GetLength(0) <= idx.x || idx.y < 0 || ary.GetLength(1) <= idx.y)
        {
            return false;
        }
        return true;
    }

    // 仲間のユニットかどうか
    bool isFriendlyUnit(UnitController unit)
    {
        if (unit && this.player == unit.player) return true;
        return false;
    }

    // 成
    public void Evolution(bool evolution)
    {
        Vector3 angle = transform.eulerAngles;

        // 成
        if (evolution && evolutionTable[unitType] != UnitType.None)
        {
            this.unitType = evolutionTable[unitType];
            angle.x = 270;
            angle.y = (0 == this.player) ? 180 : 0;
            angle.z = 0;
            transform.eulerAngles = angle; 
        }
        else
        {
            unitType = oldUnitType;
            transform.eulerAngles = getDefaultAngles(player);
        }

    }

    // キャプチャされたとき
    public void Capture(int player)
    {
        this.player = player;
        fieldStatus = FieldStatus.Captured;
        Evolution(false);
        GetComponent<Rigidbody>().isKinematic = true;
    }

    // 空いているタイルを返す
    List<Vector2Int> getEmptyTiles(UnitController[,] units)
    {
        List<Vector2Int> ret = new List<Vector2Int>();

        for (int i = 0; i < units.GetLength(0); i++)
        {
            for (int j = 0; j < units.GetLength(1); j++)
            {
                if (units[i, j]) continue;
                ret.Add(new Vector2Int(i, j));
            }
        }

        return ret;
    }

    // 成できるかどうか
    public bool isEvolution()
    {
        if (!evolutionTable.ContainsKey(unitType) || evolutionTable[unitType] == UnitType.None)
        {
            return false;
        }
        return true;
    }


}

