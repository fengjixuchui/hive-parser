// RegistryHive.cs

using System;
using System.IO;

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
                        throw new MalformedHiveException();
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
}
