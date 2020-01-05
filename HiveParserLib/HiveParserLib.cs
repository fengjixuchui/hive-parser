// HiveParserLib.cs
// Registry hive file parser implementation.

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace HiveParserLib
{
    public sealed class RegistryHive
    {
        public RegistryHive(String path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException();
            }

            this.Filepath = path;

            using (FileStream stream = File.OpenRead(path))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    Byte[] buffer = reader.ReadBytes(4);

                    if (!ValidateHiveFile(buffer))
                    {
                        throw new NotSupportedException("Specified file not a registry hive");
                    }

                    // fast forward to end of registry header block
                    reader.BaseStream.Position = 4096 + 32 + 4;

                    this.RootKey = new NodeKey(reader);
                }
            }
        }

        // ValidateHiveFile
        // Perform basic validation of hive file format.
        private Boolean ValidateHiveFile(Byte[] buffer)
        {
            return buffer[0] == 'r'
                && buffer[1] == 'e'
                && buffer[2] == 'g'
                && buffer[3] == 'f';
        }

        public String Filepath { get; set; }
        public NodeKey RootKey { get; set; }
        public Boolean WasExported { get; set; }
    }

    public sealed class NodeKey
    {
        public NodeKey(BinaryReader hive)
        {
            ReadNodeStructure(hive);
            ReadChildNodes(hive);
            ReadChildValues(hive);
        }

        // ReadNodeStructure
        // Parse basic node structure data.
        private void ReadNodeStructure(BinaryReader hive)
        {
            Byte[] buffer = hive.ReadBytes(4);

            // validate the nk header
            if (buffer[0] != 0x6E || 
                buffer[1] != 0x6B)
            {
                throw new NotSupportedException("Malformed nk header");
            }

            Int64 startingOffset = hive.BaseStream.Position;

            this.IsRootKey = (buffer[2] == 0x2C);
            this.Timestamp = DateTime.FromFileTime(hive.ReadInt64());

            // skip metadata
            hive.BaseStream.Position += 4;

            this.ParentOffset = hive.ReadInt32();
            this.SubkeyCount = hive.ReadInt32();

            // skip metadata
            hive.BaseStream.Position += 4;

            this.LFRecordOffset = hive.ReadInt32();

            // skip metadata
            hive.BaseStream.Position += 4;

            this.ValueCount = hive.ReadInt32();
            this.ValueListOffset = hive.ReadInt32();
            this.SecurityKeyOffset = hive.ReadInt32();
            this.ClassnameOffset = hive.ReadInt32();

            hive.BaseStream.Position = startingOffset + 68;

            this.NameLength = hive.ReadInt16();
            this.ClassnameLength = hive.ReadInt16();

            buffer = hive.ReadBytes(this.NameLength);
            this.Name = System.Text.Encoding.UTF8.GetString(buffer);

            hive.BaseStream.Position = this.ClassnameOffset + 4 + 4096;
            this.ClassnameData = hive.ReadBytes(this.ClassnameLength);
        }

        // ReadChildNodes
        // Parse child node data from current node.
        private void ReadChildNodes(BinaryReader hive)
        {
            // there are three distinct ways to point to child node keys:
            //  - RI -> root index
            //  - LF -> fast leaf
            //  - LH -> hash leaf

            this.ChildNodes = new List<NodeKey>();
            if (this.LFRecordOffset != -1)
            {
                hive.BaseStream.Position = 4096 + this.LFRecordOffset + 4;
                Byte[] buffer = hive.ReadBytes(2);

                // root index
                if (buffer[0] == 0x72 &&
                    buffer[1] == 0x69)
                {
                    Int16 count = hive.ReadInt16();
                    for (Int16 i = 0; i < count; ++i)
                    {
                        Int64 pos = hive.BaseStream.Position;
                        Int32 offset = hive.ReadInt32();

                        hive.BaseStream.Position = 4096 + offset + 4;
                        buffer = hive.ReadBytes(2);

                        if (!(buffer[0] == 0x6C && 
                            (buffer[1] == 0x66 || buffer[1] == 0x68)))
                        {
                            throw new Exception("Bad LF/LH record at:" + hive.BaseStream.Position);
                        }

                        ParseChildNodes(hive);

                        // go to the next record list
                        hive.BaseStream.Position = pos + 4;
                    }
                }
                // fast leaf / hash leaf
                else if (buffer[0] == 0x6C &&
                    (buffer[1] == 0x66 || buffer[1] == 0x68))
                {
                    ParseChildNodes(hive);
                }
                else
                {
                    throw new Exception("Bad Lf/LH/RI record at: " + hive.BaseStream.Position);
                }
            }
        }

        private void ParseChildNodes(BinaryReader hive)
        {
            Int16 count = hive.ReadInt16();
            Int64 topOfList = hive.BaseStream.Position;

            for (Int16 i = 0; i < count; ++i)
            {
                hive.BaseStream.Position = topOfList + (8 * i);
                Int32 newOffset = hive.ReadInt32();

                // skip metadata
                hive.BaseStream.Position += 4;
                hive.BaseStream.Position = 4096 + newOffset + 4;

                NodeKey nk = new NodeKey(hive) { ParentNodeKey = this };
                this.ChildNodes.Add(nk);
            }

            hive.BaseStream.Position = topOfList + (8 * count);
        }

        // ReadChildValues
        // Parse child value data from current node.
        private void ReadChildValues(BinaryReader hive)
        {
            this.ChildValues = new List<ValueKey>();
            if (this.ValueListOffset != -1)
            {
                hive.BaseStream.Position = 4096 + this.ValueListOffset + 4;
                for (Int32 i = 0; i < this.ValueCount; ++i)
                {
                    hive.BaseStream.Position = 4096 + this.ValueListOffset + 4 + (4 * i);
                    Int32 offset = hive.ReadInt32();
                    hive.BaseStream.Position = 4096 + offset + 4;
                    this.ChildValues.Add(new ValueKey(hive));
                }
            }
        }

        public List<NodeKey> ChildNodes { get; set; }
        public List<ValueKey> ChildValues { get; set; }
        public DateTime Timestamp { get; set; }
        public Int32 ParentOffset { get; set; }
        public Int32 SubkeyCount { get; set; }
        public Int32 LFRecordOffset { get; set; }
        public Int32 ClassnameOffset { get; set; }
        public Int32 SecurityKeyOffset { get; set; }
        public Int32 ValueCount { get; set; }
        public Int32 ValueListOffset { get; set; }
        public Int16 NameLength { get; set; }
        public Boolean IsRootKey { get; set; }
        public Int16 ClassnameLength { get; set; }
        public String Name { get; set; }
        public Byte[] ClassnameData { get; set; }
        public NodeKey ParentNodeKey { get; set; }
    }

    public sealed class ValueKey
    {
        public ValueKey(BinaryReader hive)
        {
            Byte[] buffer = hive.ReadBytes(2);

            // vk
            if (buffer[0] != 0x76 ||
                buffer[1] != 0x6B)
            {
                throw new NotSupportedException("Bad VK header");
            }

            this.NameLength = hive.ReadInt16();
            this.DataLength = hive.ReadInt32();

            Byte[] dataBuffer = hive.ReadBytes(4);

            this.ValueType = hive.ReadInt32();
            
            hive.BaseStream.Position += 4;

            buffer = hive.ReadBytes(this.NameLength);
            this.Name = (this.NameLength > 0) ?
                System.Text.Encoding.UTF8.GetString(buffer)
                : "Default";

            if (this.DataLength < 5)
            {
                this.Data = dataBuffer;
            }
            else
            {
                hive.BaseStream.Position = 4096 + BitConverter.ToInt32(dataBuffer, 0) + 4;
                this.Data = hive.ReadBytes(this.DataLength);
            }
        }

        public Int16 NameLength { get; set; }
        public Int32 DataLength { get; set; }
        public Int32 DataOffset { get; set; }
        public Int32 ValueType { get; set; }
        public String Name { get; set; }
        public Byte[] Data { get; set; }
    }

   public sealed class Utilities 
   {
        // GetBootKey
        // Extract the SYSTEM hive boot key.
        public static Byte[] GetBootKey(RegistryHive hive)
        {
            // determine the default control set used by the system
            ValueKey controlSet = GetValueKey(hive, "Select\\Default");
            Int32 cs = BitConverter.ToInt32(controlSet.Data, 0);

            // the loop below constructs the scrambled boot key
            StringBuilder scrambledKey = new StringBuilder();

            foreach (String key in new String[] { "JD", "Skew1", "GBG", "Data" })
            {
                NodeKey nk = GetNodeKey(hive, "ControlSet00" + cs + "\\Control\\Lsa\\" + key);

                for (Int32 i = 0; i < nk.ClassnameLength && i < 8; ++i)
                {
                    scrambledKey.Append((Char)nk.ClassnameData[i * 2]);
                }
            }

            Byte[] skey = StringToByteArray(scrambledKey.ToString());
            Byte[] descramble = new Byte[]
            {
                0x8, 0x5, 0x4, 0x2, 0xB, 0x9, 0xD, 0x3,
                0x0, 0x6, 0x1, 0xC, 0xE, 0xA, 0xF, 0x7
            };

            // construct the boot key
            Byte[] bootkey = new Byte[16];
            for (Int32 i = 0; i < bootkey.Length; ++i)
            {
                bootkey[i] = skey[descramble[i]];
            }

            return bootkey;
        }

        // GetValueKey
        // Get ValueKey object by path.
        public static ValueKey GetValueKey(RegistryHive hive, String path)
        {
            String[] tokens = path.Split('\\');

            // separate the node path from the value key name
            String nodePath = String.Join("\\", tokens.Take(tokens.Length - 1));
            String keyname  = tokens.Last();

            // get the node object for the desired node
            NodeKey node = GetNodeKey(hive, nodePath);

            return node.ChildValues.SingleOrDefault(v => v.Name == keyname);
        }

        // GetNodeKey
        // Get NodeKey object by path.
        public static NodeKey GetNodeKey(RegistryHive hive, String path)
        {
            NodeKey node   = hive.RootKey;
            String[] paths = path.Split('\\');

            foreach (String ch in paths)
            {
                if (String.IsNullOrEmpty(ch))
                {
                    break;
                }

                // reset flag for this path element
                Boolean found = false;

                // iterate over the children of the current node,
                // searching for the node with name of current path element
                foreach (NodeKey child in node.ChildNodes)
                {
                    if (child.Name == ch)
                    {
                        // drop to next lower level in hierarchy
                        node  = child;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    continue;
                }
                else
                {
                    throw new Exception("No child found with name: " + ch);
                }
            }

            return node;
        }

        private static Byte[] StringToByteArray(String s)
        {
            return Enumerable.Range(0, s.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(s.Substring(x, 2), 16))
                .ToArray();
        }
   }

    // TODO: define custom exceptions for the lib
}
