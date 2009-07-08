using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ZLR.VM.Debugging
{
    public class DebugInfo
    {
        private struct LineRef
        {
            public byte FileNum;
            public ushort LineNum;
            public byte Column;

            public bool IsValid
            {
                get
                {
                    if (FileNum == 0 || FileNum == 255)
                        return false;
                    else
                        return true;
                }
            }
        }

        private List<RoutineInfo> routines = new List<RoutineInfo>();
        private DoubleMap<string, byte> globals = new DoubleMap<string, byte>();
        private byte[] matchingHeader;

        public DebugInfo(Stream fromStream)
        {
            using (BinaryReader br = new BinaryReader(fromStream))
            {
                if (ReadWord(br) != 0xDEBF)
                    throw new ArgumentException("Invalid debug file header");
                if (ReadWord(br) != 0)
                    throw new ArgumentException("Unrecognized debug file version");
                ReadWord(br); // skip Inform version

                Dictionary<byte, string> filenames = new Dictionary<byte, string>(5);
                Dictionary<ushort, int> routineStarts = new Dictionary<ushort, int>();

                int i, codeArea = 0;
                byte b;
                ushort w, w2;
                string str;
                LineRef line;
                RoutineInfo routine = null;
                List<string> localList = new List<string>();
                List<LineInfo> lineList = new List<LineInfo>();
                List<ushort> offsetList = new List<ushort>();

                while (fromStream.Position < fromStream.Length)
                {
                    byte type = br.ReadByte();
                    switch (type)
                    {
                        case 0:
                            // EOF_DBR
                            fromStream.Seek(0, SeekOrigin.End);
                            break;

                        case 1:
                            // FILE_DBR
                            b = br.ReadByte();
                            ReadString(br); // skip include name
                            str = ReadString(br);
                            filenames[b] = str;
                            break;

                        case 2:
                            // CLASS_DBR
                            ReadString(br);
                            ReadLineRef(br);
                            ReadLineRef(br);
                            break;

                        case 3:
                            // OBJECT_DBR
                            ReadWord(br);
                            ReadString(br);
                            ReadLineRef(br);
                            ReadLineRef(br);
                            break;

                        case 4:
                            // GLOBAL_DBR
                            b = br.ReadByte();
                            str = ReadString(br);
                            globals.Add(str, b);
                            break;

                        case 12: // ARRAY_DBR
                        case 5: // ATTR_DBR
                        case 6: // PROP_DBR
                        case 7: // FAKE_ACTION_DBR
                        case 8: // ACTION_DBR
                            ReadWord(br);
                            ReadString(br);
                            break;

                        case 9:
                            // HEADER_DBR
                            matchingHeader = br.ReadBytes(64);
                            break;

                        case 11:
                            // ROUTINE_DBR
                            routine = new RoutineInfo();
                            routines.Add(routine);
                            w = ReadWord(br);
                            line = ReadLineRef(br);
                            if (line.IsValid)
                                routine.DefinedAt = new LineInfo(
                                    filenames[line.FileNum],
                                    line.LineNum,
                                    line.Column);
                            routine.CodeStart = ReadAddress(br);
                            routineStarts[w] = routine.CodeStart;
                            routine.Name = ReadString(br);
                            localList.Clear();
                            while ((str = ReadString(br)) != "")
                            {
                                localList.Add(str);
                            }
                            routine.Locals = localList.ToArray();
                            lineList.Clear();
                            offsetList.Clear();
                            break;

                        case 10:
                            // LINEREF_DBR
                            ReadWord(br);
                            w = ReadWord(br);
                            while (w-- > 0)
                            {
                                line = ReadLineRef(br);
                                w2 = ReadWord(br);
                                if (line.IsValid)
                                {
                                    lineList.Add(new LineInfo(
                                        filenames[line.FileNum],
                                        line.LineNum,
                                        line.Column));
                                    offsetList.Add(w2);
                                }
                            }
                            break;

                        case 14:
                            // ROUTINE_END_DBR
                            // assume routine is still set from earlier...
                            ReadWord(br); // skip routine number
                            ReadLineRef(br); // skip defn end
                            i = ReadAddress(br);
                            routine.CodeLength = i - routine.CodeStart;
                            routine.LineInfos = lineList.ToArray();
                            routine.LineOffsets = offsetList.ToArray();
                            break;

                        case 13:
                            // MAP_DBR
                            do
                            {
                                str = ReadString(br);
                                if (str != "")
                                {
                                    i = ReadAddress(br);
                                    if (str == "code area")
                                        codeArea = i;
                                }
                            } while (str != "");
                            break;
                    }
                }

                // patch routine addresses
                foreach (RoutineInfo ri in routines)
                    ri.CodeStart += codeArea;

                routines.Sort(delegate(RoutineInfo r1, RoutineInfo r2) { return r1.CodeStart - r2.CodeStart; });
            }
        }

        private static ushort ReadWord(BinaryReader rdr)
        {
            byte b1 = rdr.ReadByte();
            byte b2 = rdr.ReadByte();
            return (ushort)((b1 << 8) + b2);
        }

        private static int ReadAddress(BinaryReader rdr)
        {
            byte b1 = rdr.ReadByte();
            byte b2 = rdr.ReadByte();
            byte b3 = rdr.ReadByte();
            return (b1 << 16) + (b2 << 8) + b3;
        }

        private static string ReadString(BinaryReader rdr)
        {
            StringBuilder sb = new StringBuilder();
            byte b = rdr.ReadByte();
            while (b != 0)
            {
                sb.Append((char)b);
                b = rdr.ReadByte();
            }
            return sb.ToString();
        }

        private static LineRef ReadLineRef(BinaryReader rdr)
        {
            LineRef result;
            result.FileNum = rdr.ReadByte();
            result.LineNum = ReadWord(rdr);
            result.Column = rdr.ReadByte();
            return result;
        }

        public bool MatchesGameFile(Stream gameFile)
        {
            if (matchingHeader == null)
                return true;

            byte[] gameHeader = new byte[64];
            gameFile.Seek(0, SeekOrigin.Begin);
            int len = gameFile.Read(gameHeader, 0, 64);
            if (len < 64)
                return false;

            for (int i = 0; i < 64; i++)
                if (gameHeader[i] != matchingHeader[i])
                    return false;

            return true;
        }

        public IEnumerable<RoutineInfo> Routines
        {
            get { return routines; }
        }

        public DoubleMap<string, byte> Globals
        {
            get { return globals; }
        }

        public RoutineInfo FindRoutine(int pc)
        {
            int start = 0, end = routines.Count;

            while (start < end)
            {
                int mid = (start + end) / 2;

                RoutineInfo ri = routines[mid];
                if (pc >= ri.CodeStart && pc < ri.CodeStart + ri.CodeLength)
                    return ri;

                if (pc > ri.CodeStart)
                    start = mid + 1;
                else
                    end = mid;
            }

            return null;
        }

        public RoutineInfo FindRoutine(string name)
        {
            for (int i = 0; i < routines.Count; i++)
                if (routines[i].Name == name)
                    return routines[i];

            return null;
        }

        public LineInfo? FindLine(int pc)
        {
            RoutineInfo rtn = FindRoutine(pc);
            if (rtn == null)
                return null;

            ushort offset = (ushort)(pc - rtn.CodeStart);
            int idx = Array.BinarySearch(rtn.LineOffsets, offset);
            if (idx >= 0)
                return rtn.LineInfos[idx];

            idx = ~idx - 1;
            if (idx >= 0 && idx < rtn.LineInfos.Length)
                return rtn.LineInfos[idx];

            return null;
        }

        public int FindCodeAddress(string filename, int line)
        {
            for (int i = 0; i < routines.Count; i++)
            {
                RoutineInfo rtn = routines[i];
                for (int j = 0; j < rtn.LineInfos.Length; j++)
                    if (rtn.LineInfos[j].File == filename && rtn.LineInfos[j].Line == line)
                        return rtn.CodeStart + rtn.LineOffsets[j];
            }

            return -1;
        }
    }

    public class RoutineInfo
    {
        public string Name;
        public int CodeStart;
        public int CodeLength;
        public LineInfo DefinedAt;
        public string[] Locals;
        public ushort[] LineOffsets;
        public LineInfo[] LineInfos;
    }

    public struct LineInfo
    {
        public string File;
        public int Line;
        public int Position;

        public LineInfo(string file, int line, int position)
        {
            this.File = file;
            this.Line = line;
            this.Position = position;
        }

        public override bool Equals(object obj)
        {
            if (obj is LineInfo)
                return Equals((LineInfo)obj);
            else
                return false;
        }

        public bool Equals(LineInfo other)
        {
            return
                this.File == other.File &&
                this.Line == other.Line &&
                this.Position == other.Position;
        }

        public static bool operator ==(LineInfo a, LineInfo b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(LineInfo a, LineInfo b)
        {
            return !a.Equals(b);
        }
    }

    public class DoubleMap<K, V>
    {
        private Dictionary<K, V> forward = new Dictionary<K, V>();
        private Dictionary<V, K> backward = new Dictionary<V, K>();

        public void Add(K key, V value)
        {
            if (forward.ContainsKey(key))
                throw new InvalidOperationException("key already present");
            if (backward.ContainsKey(value))
                throw new InvalidOperationException("value already present");

            forward.Add(key, value);
            backward.Add(value, key);
        }

        public void Remove(K key)
        {
            V value;
            if (forward.TryGetValue(key, out value))
            {
                forward.Remove(key);
                backward.Remove(value);
            }
        }

        public void Remove(V value)
        {
            K key;
            if (backward.TryGetValue(value, out key))
            {
                forward.Remove(key);
                backward.Remove(value);
            }
        }

        public bool Contains(K key)
        {
            return forward.ContainsKey(key);
        }

        public bool Contains(V value)
        {
            return backward.ContainsKey(value);
        }

        public V this[K key]
        {
            get
            {
                return forward[key];
            }
        }

        public K this[V value]
        {
            get
            {
                return backward[value];
            }
        }
    }
}
