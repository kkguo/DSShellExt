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
                using (FileStream fs = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (ext == ".cia")
                        return extractIconCIA(fs);
                    else if (ext == ".3dsx")
                        return extractIcon3DSX(fs);
                    else if (ext == ".smdh")
                        return extractIconSMDH(fs);
                    else if (ext == ".nds")
                        return extractIconNDS(fs);
                    else if (ext == ".3ds")
                        return extractIcon3DS(fs);
                    else
                        return null;
                }
            }
        }

        private String _file;

        private Bitmap extractIconNDS(FileStream fs)
        {
            fs.Seek(0x68, SeekOrigin.Begin);
            byte[] buff;
            buff = new byte[4];
            fs.Read(buff, 0, buff.Length);
            UInt32 iconOffset = BitConverter.ToUInt32(buff, 0);
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
                            int b = fs.ReadByte();
                            icon[ind] = (byte)(b & 0xF);
                            icon[ind + 1] = (byte)((b & 0xF0) >> 4);
                        }
                }
            Bitmap bmp = new Bitmap(32, 32, 32, PixelFormat.Format8bppIndexed, Marshal.UnsafeAddrOfPinnedArrayElement(icon, 0));
            ColorPalette palette = bmp.Palette;
            for (int i = 0; i < 0x10; i++)
            {
                buff = new byte[2];
                fs.Read(buff, 0, buff.Length);
                //UInt16 rawpal = br.ReadUInt16();
                int r = buff[0] & 0x1F;
                int g = ((buff[1] & 0x03) << 3) + ((buff[0] & 0xE0) >> 5);
                int b = (buff[1] & 0x7C) >> 2;
                palette.Entries[i] = Color.FromArgb(255, r * 8, g * 8, b * 8);
            }
            bmp.Palette = palette;
            //bmp.MakeTransparent(bmp.Palette.Entries[0]);
            return bmp;

        }

        private Bitmap extractIconCIA(FileStream fs)
        {
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            UInt32 headerSize = BitConverter.ToUInt32(buff, 0);
            fs.Read(buff, 0, 4);
            UInt32 dummy = BitConverter.ToUInt32(buff, 0);
            fs.Read(buff, 0, 4);
            UInt32 ccSize = BitConverter.ToUInt32(buff, 0);
            fs.Read(buff, 0, 4);
            UInt32 ticketSize = BitConverter.ToUInt32(buff, 0);
            fs.Read(buff, 0, 4);
            UInt32 TMDSize = BitConverter.ToUInt32(buff, 0);
            fs.Read(buff, 0, 4);
            UInt32 metaSize = BitConverter.ToUInt32(buff, 0);
            fs.Read(buff, 0, 4);
            UInt32 contentSize = BitConverter.ToUInt32(buff, 0);
            align64bytes(ref headerSize);
            align64bytes(ref ccSize);
            align64bytes(ref ticketSize);
            align64bytes(ref TMDSize);
            align64bytes(ref contentSize);

            if (metaSize == 0) // extract icon from NCCH from content file
            {
                fs.Seek(headerSize + ccSize + ticketSize + TMDSize, SeekOrigin.Begin);
                return extractIconNCCH(fs);
            }
            else // metadata
            {
                fs.Seek(headerSize + ccSize + ticketSize + TMDSize + contentSize, SeekOrigin.Begin); // start of Meta
                fs.Seek(0x400, SeekOrigin.Current); // SMDH
                return extractIconSMDH(fs);
            }

        }

        private Bitmap extractIcon3DS(FileStream fs)
        {
            fs.Seek(0x4000, SeekOrigin.Begin);
            return extractIconNCCH(fs);
        }

        private Bitmap extractIcon3DSX(FileStream fs)
        {
            fs.Seek(4, SeekOrigin.Begin);
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            UInt32 headerSize = BitConverter.ToUInt32(buff, 0);
            if (headerSize <= 32) return null;
            fs.Seek(32, SeekOrigin.Begin);
            fs.Read(buff, 0, 4);
            UInt32 smdhOffset = BitConverter.ToUInt32(buff, 0);
            fs.Seek(smdhOffset, SeekOrigin.Begin);
            return extractIconSMDH(fs);
        }

        private Bitmap extractIconSMDH(FileStream fs)
        {
            fs.Seek(0x2040, SeekOrigin.Current); // icon
                                                 //byte[] smallicnraw = br.ReadBytes(0x480);
            fs.Seek(0x480, SeekOrigin.Current); // skip small icon
            byte[] icnrawdata = new byte[0x1200];
            fs.Read(icnrawdata, 0, icnrawdata.Length);
            return convertIcon(icnrawdata);
        }

        private Bitmap extractIconNCCH(FileStream fs)
        {
            long head = fs.Position;
            //fs.Seek(0x100, SeekOrigin.Current);// RSA-2048 signature of NCCH header
            //br.ReadUInt32(); // "NCCH"
            fs.Seek(head + 0x188, SeekOrigin.Begin);
            byte[] flags = new byte[8];
            fs.Read(flags, 0, 8);
            if ((int)(flags[7] & 0x4) != 0) // NoCrypto
            {
                fs.Seek(head + 0x1A0, SeekOrigin.Begin); // ExeFS offset
                byte[] buff = new byte[4];
                fs.Read(buff, 0, 4);
                UInt32 offset = BitConverter.ToUInt32(buff, 0);
                //UInt32 size = br.ReadUInt32();
                head += offset * 0x200;
                fs.Seek(head, SeekOrigin.Begin); // ExeFS
                buff = new byte[8];
                for (int i = 0; i < 10; i++)
                {
                    fs.Seek(head + 16 * i, SeekOrigin.Begin);
                    fs.Read(buff, 0, 8);
                    String name = System.Text.Encoding.UTF8.GetString(buff);

                    if (name.StartsWith("icon\0"))
                    {
                        buff = new byte[4];
                        fs.Read(buff, 0, 4);
                        UInt32 foffset = BitConverter.ToUInt32(buff, 0);
                        fs.Seek(head + 0x200 + foffset, SeekOrigin.Begin); // SMDH
                        return extractIconSMDH(fs);
                    }
                }                                
            } else
            {
                SharpShell.Diagnostics.Logging.Log(_file + " is encrypted");
            }
            return null;
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

        private void align64bytes(ref UInt32 val)
        {
            val = (val & (UInt32)0xFFFFFF40) + (((val & (UInt32)0x3F) == 0) ? 0 : (UInt32)0x40);
        }
    }
}
