using System;
using System.Reflection;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    public sealed class PluginEntry : Plugin
    {
        private MissionMapPointController mapController;
        private ToolStripButton topBallisticsButton;
        private ToolStrip menuStripOwner;

        public override string Name => "Майстер місії";
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
                mapController = new MissionMapPointController(Host);
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
            if (topBallisticsButton != null)
            {
                topBallisticsButton.Click -= OnTopBallisticsClick;
                if (menuStripOwner != null && menuStripOwner.Items.Contains(topBallisticsButton))
                {
                    menuStripOwner.Items.Remove(topBallisticsButton);
                }
                topBallisticsButton.Dispose();
                topBallisticsButton = null;
                menuStripOwner = null;
            }

            if (mapController != null)
            {
                mapController.Dispose();
                mapController = null;
            }

            return true;
        }

        private void TryAddTopMenuButton()
        {
            try
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

                topBallisticsButton = new ToolStripButton("Балистика")
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    ToolTipText = "Відкрити майстер балістики"
                };
                topBallisticsButton.Click += OnTopBallisticsClick;

                var insertIndex = helpBtn != null ? menuStripOwner.Items.IndexOf(helpBtn) : -1;
                if (insertIndex >= 0)
                {
                    menuStripOwner.Items.Insert(insertIndex, topBallisticsButton);
                }
                else
                {
                    menuStripOwner.Items.Add(topBallisticsButton);
                }
            }
            catch
            {
                // If UI integration changes between Mission Planner versions, keep plugin functional.
            }
        }

        private void OnTopBallisticsClick(object sender, EventArgs e)
        {
            try
            {
                WizardDialogService.OpenWizard(Host);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не вдалося відкрити вкладку Балистика:\n" + ex.Message,
                    "Балистика",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
