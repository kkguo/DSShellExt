using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;

namespace KKHomeBrews.DSShellExt
{
    class DSReader
    {
        public DSReader(String file)
        {
            _file = file;
        }

        public Bitmap Icon {
            get
            {
                String ext = Path.GetExtension(_file).ToLower();
                if (ext == ".cia")
                    return extractIconCIA();
                else if (ext == ".3dsx")
                    return extractIcon3DSX();
                else if (ext == ".smdh")
                    return extractIconSMDH();
                else if (ext == ".nds")
                    return extractIconNDS();
                else if (ext == ".3ds")
                    return extractIcon3DS();
                else
                {
                    SharpShell.Diagnostics.Logging.Log("extension is not right, Cannot extract");
                    return null;
                }
            }
        }

        private String _file;

        private Bitmap extractIcon3DS()
        {
            using (FileStream fs = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                fs.Seek(0x4000, SeekOrigin.Begin);
                return extractIconNCCH(fs);
            }
        }

        private Bitmap extractIconNDS()
        {
            using (FileStream fs = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                fs.Seek(0x68, SeekOrigin.Begin);
                UInt32 iconOffset = br.ReadUInt32();
                fs.Seek(iconOffset, SeekOrigin.Begin);
                fs.Seek(0x20, SeekOrigin.Current);
                byte[] icon = new byte[32 * 32];

                for (var tiley = 0; tiley < 4; tiley++) //tile 4x4
                    for (var tilex = 0; tilex < 4; tilex++)
                    {
                        for (var y = 0; y < 8; y++)
                            for (var x = 0; x < 8; x += 2) // every byte 2 pixels
                            {
                                int ind = (tiley * 8 + y) * 32 + tilex * 8 + x;
                                byte b = br.ReadByte();
                                icon[ind] = (byte)(b & 0xF);
                                icon[ind + 1] = (byte)((b & 0xF0) >> 4);
                            }
                    }
                Bitmap bmp = new Bitmap(32, 32, 32, PixelFormat.Format8bppIndexed, Marshal.UnsafeAddrOfPinnedArrayElement(icon, 0));
                ColorPalette palette = bmp.Palette;
                for (int i = 0; i < 0x10; i++)
                {
                    UInt16 rawpal = br.ReadUInt16();
                    int r = rawpal & 0x001F;
                    int g = (rawpal & 0x03E0) >> 5;
                    int b = (rawpal & 0x7C00) >> 10;
                    palette.Entries[i] = Color.FromArgb(255, r * 8, g * 8, b * 8);
                }
                bmp.Palette = palette;
                //bmp.MakeTransparent(bmp.Palette.Entries[0]);
                return bmp;
            }
        }

        private Bitmap extractIconCIA()
        {
            try
            {
                SharpShell.Diagnostics.Logging.Log(_file);
                using (FileStream fs = new FileStream(_file, FileMode.Open,FileAccess.Read,FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    UInt32 headerSize = br.ReadUInt32();
                    UInt32 dummy = br.ReadUInt32();
                    UInt32 ccSize = br.ReadUInt32();
                    UInt32 ticketSize = br.ReadUInt32();
                    UInt32 TMDSize = br.ReadUInt32();
                    UInt32 metaSize = br.ReadUInt32();
                    UInt32 contentSize = br.ReadUInt32();
                    align64bytes(ref headerSize);
                    align64bytes(ref ccSize);
                    align64bytes(ref ticketSize);
                    align64bytes(ref TMDSize);
                    align64bytes(ref contentSize);
                    if (metaSize == 0)
                    {
                        fs.Seek(headerSize + ccSize + ticketSize + TMDSize, SeekOrigin.Begin);
                        return extractIconNCCH(fs);
                    }
                    else
                    {
                        fs.Seek(headerSize + ccSize + ticketSize + TMDSize + contentSize, SeekOrigin.Begin); // start of Meta
                        fs.Seek(0x400, SeekOrigin.Current); // SMDH
                        return extractIconSMDH(fs);
                    }
                }
            } catch (Exception e)
            {
                SharpShell.Diagnostics.Logging.Log(e.Message);
                return null;
            }
        }

        private Bitmap extractIcon3DSX()
        {
            using (FileStream fs = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                fs.Seek(4, SeekOrigin.Begin);
                UInt32 headerSize = br.ReadUInt32();
                if (headerSize <= 32) return null;
                fs.Seek(32, SeekOrigin.Begin);
                UInt32 smdhOffset = br.ReadUInt32();
                fs.Seek(smdhOffset, SeekOrigin.Begin);
                return extractIconSMDH(fs);
            }
        }

        private Bitmap extractIconSMDH()
        {
            using (FileStream fs = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return extractIconSMDH(fs);
            }
        }

        private Bitmap extractIconSMDH(FileStream fs)
        {
            SharpShell.Diagnostics.Logging.Log("Insider SMDH");
            fs.Seek(0x2040, SeekOrigin.Current); // icon
                                                 //byte[] smallicnraw = br.ReadBytes(0x480);
            fs.Seek(0x480, SeekOrigin.Current); // skip small icon
            byte[] icnrawdata = new byte[0x1200];
            fs.Read(icnrawdata, 0, icnrawdata.Length);
            return convertIcon(icnrawdata);
        }

        private Bitmap extractIconNCCH(FileStream fs)
        {
            using (BinaryReader br = new BinaryReader(fs, Encoding.Default, true))
            {
                long head = fs.Position;
                //fs.Seek(0x100, SeekOrigin.Current);// RSA-2048 signature of NCCH header
                //br.ReadUInt32(); // "NCCH"
                fs.Seek(head + 0x188, SeekOrigin.Begin);
                byte[] flags = br.ReadBytes(8);
                if ((int)(flags[7] & 0x8) > 0) // NoCrypto
                {
                    fs.Seek(head + 0x1A0, SeekOrigin.Begin); // ExeFS offset
                    UInt32 offset = br.ReadUInt32();
                    //UInt32 size = br.ReadUInt32();
                    head += offset * 0x200;
                    fs.Seek(head, SeekOrigin.Begin); // ExeFS
                    for (int i = 0; i < 10; i++)
                    {
                        fs.Seek(head + 16 * i, SeekOrigin.Begin);
                        String name = System.Text.Encoding.Default.GetString(br.ReadBytes(8));

                        if (name.StartsWith("icon\0"))
                        {
                            UInt32 foffset = br.ReadUInt32();
                            fs.Seek(head + 0x200 + foffset, SeekOrigin.Begin); // SMDH
                            return extractIconSMDH(fs);
                        }
                    }
                }
                return null;
            }
        }

        private Bitmap convertIcon(byte[] rawdata)
        {
            byte[] icndata = new byte[rawdata.Length];
            MemoryStream raw = new MemoryStream(rawdata);

            int iconSize = (int)Math.Sqrt(raw.Length / 2); // assume icon is square.
            for (var tiley = 0; tiley < iconSize / 8; tiley++) // tile is 8x8
                for (var tilex = 0; tilex < iconSize / 8; tilex++)
                {
                    for (var y = 0; y < 2; y++)
                        for (var x = 0; x < 2; x++)
                        {
                            for (var yy = 0; yy < 2; yy++)
                                for (var xx = 0; xx < 2; xx++)
                                {
                                    for (var yyy = 0; yyy < 2; yyy++)
                                        for (var xxx = 0; xxx < 2; xxx++)
                                        {
                                            int ind = ((tiley * 8 + y * 4 + yy * 2 + yyy) * iconSize + (tilex * 8 + x * 4 + xx * 2 + xxx)) * 2;
                                            icndata[ind] = (byte)raw.ReadByte();
                                            icndata[ind + 1] = (byte)raw.ReadByte();
                                        }
                                }
                        }
                }
            return new Bitmap(iconSize, iconSize, 2 * iconSize, PixelFormat.Format16bppRgb565, Marshal.UnsafeAddrOfPinnedArrayElement(icndata, 0));
        }

        private void align64bytes(ref UInt32 size)
        {
            size = (size & (UInt32)0xFFFFFF40) + (((size & (UInt32)0x3F) == 0) ? 0 : (UInt32)0x40);
        }
    }
}
