﻿using System;
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

namespace KKHomeBrews.ThreeDSShellExt
{
    class ThreeDSReader
    {
        public ThreeDSReader(String file)
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
                else
                {
                    SharpShell.Diagnostics.Logging.Log("extension is not right, Cannot extract");
                    return null;
                }
            }
        }

        private String _file;
        public Bitmap extractIconCIA()
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

                    if (metaSize == 0)
                    {
                        SharpShell.Diagnostics.Logging.Log("metasize == 0");
                        return null;
                    }
                    UInt32 contentSize = br.ReadUInt32();
                    align64bytes(ref headerSize);
                    align64bytes(ref ccSize);
                    align64bytes(ref ticketSize);
                    align64bytes(ref TMDSize);
                    align64bytes(ref contentSize);
                    fs.Seek(headerSize + ccSize + ticketSize + TMDSize + contentSize, SeekOrigin.Begin); // start of Meta
                    fs.Seek(0x400, SeekOrigin.Current); // SMDH

                    return extractIconSMDH(fs);
                }
            } catch (Exception e)
            {
                SharpShell.Diagnostics.Logging.Log(e.Message);
                return null;
            }
        }

        public Bitmap extractIcon3DSX()
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

        public Bitmap extractIconSMDH()
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
            byte[] icndata = new byte[icnrawdata.Length];
            MemoryStream raw = new MemoryStream(icnrawdata);

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
                                            icndata[((tiley * 8 + y * 4 + yy * 2 + yyy) * iconSize + (tilex * 8 + x * 4 + xx * 2 + xxx)) * 2] = (byte)raw.ReadByte();
                                            icndata[((tiley * 8 + y * 4 + yy * 2 + yyy) * iconSize + (tilex * 8 + x * 4 + xx * 2 + xxx)) * 2 + 1] = (byte)raw.ReadByte();
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