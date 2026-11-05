using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CK.SemesterProject.LevelDesign.WindPuzzle
{
    /// <summary>바람길 퍼즐 판 정의(퍼즐 코드 P1-… 에서 만든다).</summary>
    public sealed class WindPuzzleLevel
    {
        public struct Vent { public int Id; public int X; public int Y; public int Side; }
        public struct Exit { public int X; public int Y; public int Side; }

        public string Name;
        public int W;
        public int H;
        public char[,] Grid; // [y, x]
        public readonly List<Vent> Vents = new List<Vent>();
        public readonly List<Exit> Exits = new List<Exit>();
        public readonly List<string> ButtonKeys = new List<string>();
        public readonly Dictionary<string, int[]> Buttons = new Dictionary<string, int[]>();
        public readonly List<Vector2Int> Boxes = new List<Vector2Int>();
    }

    /// <summary>판 상태. 상자가 출구로 나가면 null.</summary>
    public sealed class WindPuzzleState
    {
        public char[,] Grid;
        public Vector2Int?[] Boxes;

        public WindPuzzleState Clone()
        {
            return new WindPuzzleState { Grid = (char[,])Grid.Clone(), Boxes = (Vector2Int?[])Boxes.Clone() };
        }
    }

    /// <summary>
    /// 바람길 퍼즐 규칙. 기획 프로토타입(wind_puzzle HTML)의 엔진을 그대로 옮겼다.
    /// 규칙 가정: 맞바람은 상쇄(cancel), 바람은 상자에서 멈춘다.
    /// </summary>
    public static class WindPuzzleEngine
    {
        public const string CodePrefix = "P1-";
        public static readonly int[] DX = { 0, 1, 0, -1 };
        public static readonly int[] DY = { -1, 0, 1, 0 };

        public struct Hit { public int Box; public int Entry; public int T; public int Vent; }
        public sealed class WindTrace { public int Vent; public readonly List<Vector2> Points = new List<Vector2>(); public readonly List<Hit> Hits = new List<Hit>(); }
        public sealed class BoxMove { public int Box; public readonly List<Vector2> Path = new List<Vector2>(); public bool Exited; }
        public sealed class PressResult { public WindPuzzleState State; public readonly List<WindTrace> Traces = new List<WindTrace>(); public readonly List<BoxMove> Moves = new List<BoxMove>(); }

        public static int Opp(int d) => (d + 2) % 4;
        public static int Bit(int d) => 1 << d;

        public static int Mask(char c)
        {
            switch (c)
            {
                case '-': return 2 | 8;
                case '|': return 1 | 4;
                case 'L': return 1 | 2;
                case 'F': return 2 | 4;
                case '7': return 4 | 8;
                case 'J': return 8 | 1;
                default: return 0;
            }
        }

        public static bool IsBend(char c) => c == 'L' || c == 'F' || c == '7' || c == 'J';

        public static char RotateCW(char c)
        {
            switch (c)
            {
                case 'L': return 'F';
                case 'F': return '7';
                case '7': return 'J';
                case 'J': return 'L';
                default: return c;
            }
        }

        public static WindPuzzleState InitialState(WindPuzzleLevel level)
        {
            var boxes = new Vector2Int?[level.Boxes.Count];
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i] = level.Boxes[i];
            }
            return new WindPuzzleState { Grid = (char[,])level.Grid.Clone(), Boxes = boxes };
        }

        private static bool InBounds(WindPuzzleLevel l, int x, int y) => x >= 0 && y >= 0 && x < l.W && y < l.H;
        private static int MaskAt(WindPuzzleLevel l, WindPuzzleState s, int x, int y) => InBounds(l, x, y) ? Mask(s.Grid[y, x]) : 0;

        public static int BoxAt(WindPuzzleState s, int x, int y)
        {
            for (int i = 0; i < s.Boxes.Length; i++)
            {
                if (s.Boxes[i].HasValue && s.Boxes[i].Value.x == x && s.Boxes[i].Value.y == y)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int OtherSide(int mask, int entry)
        {
            for (int d = 0; d < 4; d++)
            {
                if (d != entry && (mask & Bit(d)) != 0)
                {
                    return d;
                }
            }
            return -1;
        }

        private static bool IsExit(WindPuzzleLevel l, int x, int y, int d)
        {
            foreach (WindPuzzleLevel.Exit e in l.Exits)
            {
                if (e.X == x && e.Y == y && e.Side == d)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>바람 추적. Points는 격자 좌표(칸 중심 = +0.5)로 연출에 쓴다.</summary>
        public static WindTrace TraceWind(WindPuzzleLevel l, WindPuzzleState s, WindPuzzleLevel.Vent vent)
        {
            var trace = new WindTrace { Vent = vent.Id };
            int x = vent.X, y = vent.Y, entry = vent.Side, t = 0;
            var seen = new HashSet<int>();
            while (InBounds(l, x, y))
            {
                int m = MaskAt(l, s, x, y);
                if ((m & Bit(entry)) == 0)
                {
                    break;
                }
                int key = (y * l.W + x) * 4 + entry;
                if (!seen.Add(key))
                {
                    break;
                }
                trace.Points.Add(new Vector2(x + 0.5f + DX[entry] * 0.5f, y + 0.5f + DY[entry] * 0.5f));
                int bi = BoxAt(s, x, y);
                if (bi >= 0)
                {
                    trace.Points.Add(new Vector2(x + 0.5f, y + 0.5f));
                    trace.Hits.Add(new Hit { Box = bi, Entry = entry, T = t, Vent = vent.Id });
                    break;
                }
                int ex = OtherSide(m, entry);
                if (DX[entry] + DX[ex] == 0 && DY[entry] + DY[ex] == 0)
                {
                    trace.Points.Add(new Vector2(x + 0.5f, y + 0.5f));
                }
                else
                {
                    float ox = x + 0.5f + (DX[entry] + DX[ex]) * 0.5f, oy = y + 0.5f + (DY[entry] + DY[ex]) * 0.5f;
                    float a1 = Mathf.Atan2(DY[entry] * 0.5f + y + 0.5f - oy, DX[entry] * 0.5f + x + 0.5f - ox);
                    float d = Mathf.Atan2(DY[ex] * 0.5f + y + 0.5f - oy, DX[ex] * 0.5f + x + 0.5f - ox) - a1;
                    while (d > Mathf.PI) d -= 2f * Mathf.PI;
                    while (d < -Mathf.PI) d += 2f * Mathf.PI;
                    for (int i = 1; i < 6; i++)
                    {
                        trace.Points.Add(new Vector2(ox + 0.5f * Mathf.Cos(a1 + d * i / 6f), oy + 0.5f * Mathf.Sin(a1 + d * i / 6f)));
                    }
                }
                trace.Points.Add(new Vector2(x + 0.5f + DX[ex] * 0.5f, y + 0.5f + DY[ex] * 0.5f));
                x += DX[ex];
                y += DY[ex];
                entry = Opp(ex);
                t++;
            }
            return trace;
        }

        private static BoxMove SlideBox(WindPuzzleLevel l, WindPuzzleState s, int bi, int entry)
        {
            Vector2Int start = s.Boxes[bi].Value;
            int x = start.x, y = start.y;
            var move = new BoxMove { Box = bi };
            move.Path.Add(new Vector2(x + 0.5f, y + 0.5f));
            var seen = new HashSet<int>();
            while (true)
            {
                int ex = OtherSide(MaskAt(l, s, x, y), entry);
                if (ex < 0)
                {
                    break;
                }
                if (IsExit(l, x, y, ex))
                {
                    move.Exited = true;
                    move.Path.Add(new Vector2(x + 0.5f + DX[ex] * 1.2f, y + 0.5f + DY[ex] * 1.2f));
                    break;
                }
                int nx = x + DX[ex], ny = y + DY[ex];
                if (!InBounds(l, nx, ny) || (MaskAt(l, s, nx, ny) & Bit(Opp(ex))) == 0 || BoxAt(s, nx, ny) >= 0)
                {
                    break;
                }
                int key = (ny * l.W + nx) * 4 + ex;
                if (!seen.Add(key))
                {
                    break;
                }
                x = nx;
                y = ny;
                entry = Opp(ex);
                move.Path.Add(new Vector2(x + 0.5f, y + 0.5f));
            }
            s.Boxes[bi] = move.Exited ? (Vector2Int?)null : new Vector2Int(x, y);
            return move;
        }

        public static PressResult PressButton(WindPuzzleLevel l, WindPuzzleState s0, string key)
        {
            var result = new PressResult { State = s0.Clone() };
            WindPuzzleState s = result.State;
            if (!l.Buttons.TryGetValue(key, out int[] ids))
            {
                return result;
            }
            var sorted = new List<int>(ids);
            sorted.Sort();
            foreach (int id in sorted)
            {
                foreach (WindPuzzleLevel.Vent v in l.Vents)
                {
                    if (v.Id == id)
                    {
                        result.Traces.Add(TraceWind(l, s, v));
                        break;
                    }
                }
            }
            var byBox = new SortedDictionary<int, List<Hit>>();
            foreach (WindTrace tr in result.Traces)
            {
                foreach (Hit h in tr.Hits)
                {
                    if (!byBox.TryGetValue(h.Box, out List<Hit> list))
                    {
                        byBox[h.Box] = list = new List<Hit>();
                    }
                    list.Add(h);
                }
            }
            var plan = new List<Hit>();
            foreach (KeyValuePair<int, List<Hit>> pair in byBox)
            {
                // 맞바람(서로 다른 면에서 닿음)은 상쇄된다.
                bool sameSide = true;
                foreach (Hit h in pair.Value)
                {
                    sameSide &= h.Entry == pair.Value[0].Entry;
                }
                if (!sameSide)
                {
                    continue;
                }
                Hit chosen = pair.Value[0];
                foreach (Hit h in pair.Value)
                {
                    if (h.T < chosen.T)
                    {
                        chosen = h;
                    }
                }
                plan.Add(chosen);
            }
            plan.Sort((a, b) =>
            {
                int c = a.T.CompareTo(b.T);
                if (c != 0) return c;
                Vector2Int pa = s.Boxes[a.Box].Value, pb = s.Boxes[b.Box].Value;
                return (pa.y * l.W + pa.x).CompareTo(pb.y * l.W + pb.x);
            });
            foreach (Hit h in plan)
            {
                BoxMove move = SlideBox(l, s, h.Box, h.Entry);
                if (move.Path.Count > 1 || move.Exited)
                {
                    result.Moves.Add(move);
                }
            }
            return result;
        }

        public static bool CanRotate(WindPuzzleLevel l, WindPuzzleState s, int x, int y) => InBounds(l, x, y) && IsBend(s.Grid[y, x]) && BoxAt(s, x, y) < 0;

        public static WindPuzzleState Rotate(WindPuzzleState s0, int x, int y)
        {
            WindPuzzleState s = s0.Clone();
            s.Grid[y, x] = RotateCW(s.Grid[y, x]);
            return s;
        }

        public static bool IsWon(WindPuzzleState s)
        {
            foreach (Vector2Int? b in s.Boxes)
            {
                if (b.HasValue)
                {
                    return false;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ 퍼즐 코드

        /// <summary>HTML 에디터의 퍼즐 코드(P1-…)를 판으로 바꾼다. 형식이 틀리면 예외.</summary>
        public static WindPuzzleLevel Decode(string code)
        {
            code = (code ?? string.Empty).Trim();
            int hash = code.IndexOf("#p=", StringComparison.Ordinal);
            if (hash >= 0)
            {
                code = Uri.UnescapeDataString(code.Substring(hash + 3));
            }
            if (!code.StartsWith(CodePrefix, StringComparison.Ordinal))
            {
                throw new FormatException("퍼즐 코드는 P1- 로 시작해야 합니다.");
            }
            string b64 = code.Substring(CodePrefix.Length).Replace('-', '+').Replace('_', '/');
            while (b64.Length % 4 != 0)
            {
                b64 += "=";
            }
            string json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            int index = 0;
            var root = ParseJson(json, ref index) as List<object>;
            if (root == null || root.Count < 8)
            {
                throw new FormatException("퍼즐 코드 형식이 올바르지 않습니다.");
            }
            var level = new WindPuzzleLevel
            {
                Name = root[0] as string ?? string.Empty,
                W = ToInt(root[1]),
                H = ToInt(root[2]),
            };
            string tiles = root[3] as string;
            if (tiles == null || tiles.Length != level.W * level.H)
            {
                throw new FormatException("타일 수가 판 크기와 맞지 않습니다.");
            }
            level.Grid = new char[level.H, level.W];
            for (int y = 0; y < level.H; y++)
            {
                for (int x = 0; x < level.W; x++)
                {
                    level.Grid[y, x] = tiles[y * level.W + x];
                }
            }
            foreach (object o in (List<object>)root[4])
            {
                int c = ToInt(o);
                level.Boxes.Add(new Vector2Int(c % level.W, c / level.W));
            }
            foreach (object o in (List<object>)root[5])
            {
                int v = ToInt(o), c = v >> 2;
                level.Exits.Add(new WindPuzzleLevel.Exit { X = c % level.W, Y = c / level.W, Side = v & 3 });
            }
            var vents = (List<object>)root[6];
            for (int i = 0; i < vents.Count; i++)
            {
                int v = ToInt(vents[i]), c = v >> 2;
                level.Vents.Add(new WindPuzzleLevel.Vent { Id = i + 1, X = c % level.W, Y = c / level.W, Side = v & 3 });
            }
            foreach (object o in (List<object>)root[7])
            {
                var pair = (List<object>)o;
                string key = (string)pair[0];
                var ids = new List<int>();
                foreach (object id in (List<object>)pair[1])
                {
                    int value = ToInt(id);
                    if (value >= 1 && value <= vents.Count)
                    {
                        ids.Add(value);
                    }
                }
                level.ButtonKeys.Add(key);
                level.Buttons[key] = ids.ToArray();
            }
            return level;
        }

        private static int ToInt(object o) => Convert.ToInt32(o is double d ? Math.Round(d) : o);

        // 퍼즐 코드에 쓰이는 만큼만 다루는 작은 JSON 파서(배열·문자열·숫자).
        private static object ParseJson(string s, ref int i)
        {
            SkipWhite(s, ref i);
            char c = s[i];
            if (c == '[')
            {
                i++;
                var list = new List<object>();
                SkipWhite(s, ref i);
                if (s[i] == ']')
                {
                    i++;
                    return list;
                }
                while (true)
                {
                    list.Add(ParseJson(s, ref i));
                    SkipWhite(s, ref i);
                    if (s[i] == ',')
                    {
                        i++;
                        continue;
                    }
                    if (s[i] == ']')
                    {
                        i++;
                        return list;
                    }
                    throw new FormatException("JSON 배열 형식 오류");
                }
            }
            if (c == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (s[i] != '"')
                {
                    if (s[i] == '\\')
                    {
                        i++;
                        char e = s[i];
                        switch (e)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; break;
                            default: sb.Append(e); break;
                        }
                    }
                    else
                    {
                        sb.Append(s[i]);
                    }
                    i++;
                }
                i++;
                return sb.ToString();
            }
            int start = i;
            while (i < s.Length && "-+.0123456789eE".IndexOf(s[i]) >= 0)
            {
                i++;
            }
            if (start == i)
            {
                throw new FormatException("JSON 값 형식 오류");
            }
            return double.Parse(s.Substring(start, i - start), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void SkipWhite(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
        }
    }
}
