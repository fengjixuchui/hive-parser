// ConsoleClient.cs
// Simple console client application for registry hive parsing library.

using System;
using HiveParserLib;

namespace ConsoleClient
{
    class ConsoleClient
    {
        static void Main(string[] args)
        {
            RegistryHive hive = new RegistryHive(args[0]);
            Console.WriteLine("The root key's name is: " + hive.RootKey.Name);

            Byte[] bootkey = null;

            try
            {
                bootkey = Utilities.GetBootKey(hive);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception thrown by GetBootKey():");
                Console.WriteLine(e.Message);
            }

            if (bootkey != null)
            {
                Console.WriteLine("Boot key: " + BitConverter.ToString(bootkey));
            }
        }
    }
}
