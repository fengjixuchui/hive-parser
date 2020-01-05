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

            GetInstalledSoftware(hive);
        }

        static void GetBootKey(RegistryHive hive)
        {
            Byte[] bootkey = null;

            try
            {
                bootkey = Utilities.GetBootKey(hive);
            }
            catch (HiveParserLibException e)
            {
                Console.WriteLine("Failed to extract boot key:");
                Console.WriteLine(e.Message);
            }

            if (bootkey != null)
            {
                Console.WriteLine("Boot key: " + BitConverter.ToString(bootkey));
            }
        }

        static void GetInstalledSoftware(RegistryHive hive)
        {
            Utilities.GetInstalledSoftware(hive);
        }
    }
}
