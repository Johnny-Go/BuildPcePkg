using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BuildPcePkg
{
    class Program
    {
        /// <summary>
        /// description of pce.pkg by 'hippy dave' on GBATemp.net (https://gbatemp.net/threads/turbografx-16-pc-engine- ection-guide.434515/)
        /// All the sizes are stored as four bytes, in reverse byte order.       
        /// - The first four bytes are the total size of the pce.pkg file minus 4 (ie the size of the rest of the file after the first four bytes) - so you can put this in last.
        /// - Then there are three other files stored, in the format: (four byte size of data section) (filename followed by null 0x00 byte) (data section)... so you have:
        /// -- pceconfig.bin, which has its size listed as 0x000000A0 bytes(160 bytes), followed by its filename and a 0x00 byte, then the 0xa0 bytes from offset 0x16 to 0xb5 are its data.It's mostly zeros with the filename of the rom listed twice at particular locations, you can safely overwrite the filename even if it's longer(within reason).
        /// -- then two copies of the same thing: the rom file stored in the previously described format...the rom filesize (which is where you saw that changed byte, because 0x00080000 is 512KB and 0x00100000 is 1MB), the rom filename(as listed in pceconfig.bin) followed by a 0x00 byte, and then the rom data. Then rom size/name/data is repeated a second time and that's the end of the file.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            //make sure proper number of aruments were passed
            if (args.Length != 1)
            {
                Console.WriteLine("Invalid arguments, only argument is the ROM to use for pce.pkg creation");
                return;
            }
            var filePath = args[0];

            //verify we can find the file
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Can't find the specified file");
                return;
            }

            //get the rom as a byte array, and get the rom name
            byte[] romData;
            string romName;
            using (var data = new MemoryStream())
            {
                using (var file = File.OpenRead(filePath))
                {
                    file.CopyTo(data);
                    romData = data.ToArray();
                    var pathParts = file.Name.Split('\\');
                    romName = pathParts[pathParts.Length - 1];
                }
            }

            //verify the rom name is short enough
            if (romName.Length > 64)
            {
                Console.WriteLine("Shorten full ROM name to 64 characters");
                return;
            }

            //convert the rom name to a byte array
            byte[] romNameByteArray = Encoding.ASCII.GetBytes(romName);

            //build the file data to write
            var toWrite = new List<byte>();
            toWrite.AddRange(PceConfigData(romNameByteArray));
            toWrite.AddRange(RomData(romData, romNameByteArray));
            var arrayToWrite = toWrite.ToArray();

            //write the file
            using (var fs = new FileStream("pce.pkg", FileMode.Create, FileAccess.ReadWrite))
            {
                //write the file length data
                fs.Write(GetLengthAsByteArrayInReverseOrder(arrayToWrite.Length), 0, 4);
                fs.Write(arrayToWrite, 0, arrayToWrite.Length);
            }
        }

        private static List<byte> PceConfigData(byte[] romNameByteArray)
        {
            var pceConfigData = new List<byte>();
            var romNameLength = (64 - romNameByteArray.Length);

            //write the pceconfig data
            pceConfigData.AddRange(new byte[] { 160, 0, 0, 0 }); //"A0000000" (specifying the pceconfig data is 160 bytes long)
            pceConfigData.AddRange(new byte[] { 112, 99, 101, 99, 111, 110, 102, 105, 103, 46, 98, 105, 110, 0 }); //"706365636F6E6669672E62696E00" (pceconfig.bin) followed by null 0x00 byte
            //write 32 0x00s (no idea why this is done)
            pceConfigData.AddRange(Enumerable.Repeat<byte>((byte)0, 32));
            //write the 64 character rom name
            pceConfigData.AddRange(romNameByteArray);
            pceConfigData.AddRange(Enumerable.Repeat<byte>((byte)0, romNameLength));
            //write the 64 character rom name a second time
            pceConfigData.AddRange(romNameByteArray);
            pceConfigData.AddRange(Enumerable.Repeat<byte>((byte)0, romNameLength));

            return pceConfigData;
        }

        private static List<byte> RomData(byte[] romDataByteArray, byte[] romNameByteArray)
        {
            var romData = new List<byte>();

            var reveresedLength = GetLengthAsByteArrayInReverseOrder(romDataByteArray.Length);

            //write the rom data
            romData.AddRange(reveresedLength);
            romData.AddRange(romNameByteArray);
            romData.Add((byte)0);
            romData.AddRange(romDataByteArray);

            //write the rom data a second time
            romData.AddRange(reveresedLength);
            romData.AddRange(romNameByteArray);
            romData.Add((byte)0);
            romData.AddRange(romDataByteArray);

            return romData;
        }

        //get the length as a byte array in reverse order
        private static byte[] GetLengthAsByteArrayInReverseOrder(int length)
        {
            byte[] bytes = new byte[4];

            bytes[3] = (byte)(length >> 24);
            bytes[2] = (byte)(length >> 16);
            bytes[1] = (byte)(length >> 8);
            bytes[0] = (byte)length;

            return bytes;
        }
    }
}