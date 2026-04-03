using System;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    internal static class WizardDialogService
    {
        public static void OpenWizard(PluginHost host)
        {
            try
            {
                using (var wizard = new MissionWizardForm(host))
                {
                    var owner = host?.MainForm as IWin32Window;
                    if (owner != null)
                    {
                        wizard.ShowDialog(owner);
                    }
                    else
                    {
                        wizard.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не вдалося відкрити Майстер місії:\n" + ex.Message,
                    "Майстер місії",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}