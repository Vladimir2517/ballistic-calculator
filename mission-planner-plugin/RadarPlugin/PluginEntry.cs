using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace RadarPlugin
{
    public sealed class PluginEntry : Plugin
    {
        private ToolStripButton topRadarButton;
        private ToolStrip menuStripOwner;
        private Image radarIcon;
        private RadarOverlayController radarController;

        public override string Name => "Радар";
        public override string Version => "0.1.0";
        public override string Author => "Vladimir2517";

        public override bool Init()
        {
            return true;
        }

        public override bool Loaded()
        {
            try
            {
                radarController = new RadarOverlayController(Host);
                TryAddTopMenuButton();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool Loop()
        {
            return true;
        }

        public override bool Exit()
        {
            if (topRadarButton != null)
            {
                topRadarButton.Click -= OnTopRadarClick;
                if (menuStripOwner != null && menuStripOwner.Items.Contains(topRadarButton))
                {
                    menuStripOwner.Items.Remove(topRadarButton);
                }

                topRadarButton.Dispose();
                topRadarButton = null;
                menuStripOwner = null;
            }

            if (radarIcon != null)
            {
                radarIcon.Dispose();
                radarIcon = null;
            }

            if (radarController != null)
            {
                radarController.Dispose();
                radarController = null;
            }

            return true;
        }

        private void TryAddTopMenuButton()
        {
            var mainForm = Host?.MainForm;
            if (mainForm == null)
            {
                return;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var helpBtn = mainForm.GetType().GetField("MenuHelp", flags)?.GetValue(mainForm) as ToolStripItem;
            var plannerBtn = mainForm.GetType().GetField("MenuFlightPlanner", flags)?.GetValue(mainForm) as ToolStripItem;
            var dataBtn = mainForm.GetType().GetField("MenuFlightData", flags)?.GetValue(mainForm) as ToolStripItem;

            menuStripOwner = (helpBtn?.Owner as ToolStrip)
                ?? (plannerBtn?.Owner as ToolStrip)
                ?? (dataBtn?.Owner as ToolStrip);

            if (menuStripOwner == null)
            {
                return;
            }

            radarIcon = CreateRadarIcon();
            topRadarButton = new ToolStripButton("Радар")
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                Image = radarIcon,
                ImageTransparentColor = Color.Transparent,
                ToolTipText = "Відкрити вкладку Радар"
            };
            topRadarButton.Click += OnTopRadarClick;

            var insertIndex = GetInsertIndexAfterHelp(menuStripOwner, helpBtn);
            if (insertIndex >= 0)
            {
                menuStripOwner.Items.Insert(insertIndex, topRadarButton);
            }
            else
            {
                menuStripOwner.Items.Add(topRadarButton);
            }
        }

        private static int GetInsertIndexAfterHelp(ToolStrip owner, ToolStripItem helpItem)
        {
            if (owner == null)
            {
                return -1;
            }

            if (helpItem != null)
            {
                var idx = owner.Items.IndexOf(helpItem);
                if (idx >= 0)
                {
                    return idx + 1;
                }
            }

            for (var i = 0; i < owner.Items.Count; i++)
            {
                var text = owner.Items[i].Text ?? string.Empty;
                if (text.Trim().Equals("HELP", StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1;
                }
            }

            return -1;
        }

        private static Image CreateRadarIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            using (var penRing = new Pen(Color.FromArgb(54, 138, 92), 1f))
            using (var penSweep = new Pen(Color.FromArgb(108, 196, 96), 1.5f))
            using (var penGrid = new Pen(Color.FromArgb(60, 100, 80), 1f))
            using (var textBrush = new SolidBrush(Color.FromArgb(88, 170, 105)))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                g.Clear(Color.Transparent);

                g.DrawEllipse(penRing, 2, 2, 12, 12);
                g.DrawEllipse(penGrid, 5, 5, 6, 6);
                g.DrawLine(penGrid, 8, 2, 8, 14);
                g.DrawLine(penGrid, 2, 8, 14, 8);
                g.DrawLine(penSweep, 8, 8, 12, 4);
                g.FillEllipse(textBrush, 10, 5, 2, 2);

                using (var font = new Font("Arial", 3f, FontStyle.Bold))
                {
                    var text = "RADAR";
                    var textSize = g.MeasureString(text, font);
                    var textX = (16 - textSize.Width) / 2;
                    g.DrawString(text, font, textBrush, textX, 12);
                }
            }

            return bmp;
        }

        private void OnTopRadarClick(object sender, EventArgs e)
        {
            if (radarController != null)
            {
                radarController.ActivateRadarTab();
                return;
            }
        }
    }
}
