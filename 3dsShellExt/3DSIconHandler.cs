﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using SharpShell.Attributes;
using SharpShell.Diagnostics;
using SharpShell.Extensions;
using SharpShell.ServerRegistration;
using SharpShell.SharpIconHandler;
using System.Runtime.InteropServices;
using System.Drawing;

namespace KKHomeBrews.ThreeDSShellExt
{
    [Guid("c4f7c2cc-9937-4f7c-bf67-1de48dd6f556")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".cia")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".3dsx")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".smdh")]
    public class IconHandler : SharpIconHandler
    {
        protected override Icon GetIcon(bool smallIcon, uint iconSize)
        {
            ThreeDSReader reader = new ThreeDSReader(SelectedItemPath);
            Bitmap bmp = reader.Icon;
            return Icon.FromHandle(ResizeBitmap(reader.Icon,new Size((int)iconSize,(int)iconSize)).GetHicon());
        }

        [CustomRegisterFunction]
        public static void postDoRegister(Type type, RegistrationType registrationType)
        {
            Console.WriteLine("Registering " + type.FullName + " Version" + type.Assembly.GetName().Version.ToString());
            #region Enable debug log
#if DEBUG
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE", true).CreateSubKey(@"SharpShell", RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                key.SetValue("LoggingMode", 4);
                key.SetValue("LogPath", @"%AppData%\3dsshellext.log", RegistryValueKind.ExpandString);
            }
#endif
            #endregion
        }

        private Bitmap ResizeBitmap(Bitmap original, Size size)
        {
            // Get better image while stretch
            if (original != null)
            {
                Bitmap b = new Bitmap(size.Width, size.Height);
                using (Graphics g = Graphics.FromImage((Image)b))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                    g.DrawImage(original, 0, 0, size.Width, size.Height);
                }
                return b;
            }
            else
            {
                return null;
            }
        }
    }
}