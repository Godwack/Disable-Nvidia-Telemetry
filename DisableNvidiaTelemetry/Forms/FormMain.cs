﻿#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Windows.Forms;
using DisableNvidiaTelemetry.Controls;
using DisableNvidiaTelemetry.Properties;
using DisableNvidiaTelemetry.Utilities;
using ExtendedVersion;
using log4net.Core;
using Microsoft.Win32.TaskScheduler;

#endregion

namespace DisableNvidiaTelemetry.Forms
{
    public partial class FormMain : Form
    {
        private readonly TelemetryControl _servicesControl;
        private readonly TelemetryControl _tasksControl;

        private bool _ignoreTaskSetting;
        private List<ServiceController> _telemetryServices = new List<ServiceController>();
        private List<Task> _telemetryTasks = new List<Task>();

        public FormMain()
        {
            InitializeComponent();

            _tasksControl = new TelemetryControl("Telemetry Tasks") {Dock = DockStyle.Top};
            _tasksControl.CheckStateChanged += telemControl_CheckStateChanged;
            tabPage1.Controls.Add(_tasksControl);
            _servicesControl = new TelemetryControl("Telemetry Services") {Dock = DockStyle.Top};
            _servicesControl.CheckStateChanged += telemControl_CheckStateChanged;
            tabPage1.Controls.Add(_servicesControl);

            txtLicense.Text = Resources.ApplicationLicense;

            LogExtensions.LogEvent += OnLogEvent;
            UpdaterUtilities.UpdateResponse += UpdaterUtilities_UpdateResponse;

            CheckBackgroundTask();

            chkFileLogging.Checked = Settings.Default.FileLogging;
            chkUpdates.Checked = Settings.Default.StartupUpdate;
            cbTaskTrigger.SelectedIndex = Settings.Default.BackgroundTaskTrigger;

            if (Settings.Default.StartupUpdate)
            {
                btnUpdatecheck.Enabled = false;
                UpdaterUtilities.UpdateCheck(false);
            }

            var version = GetVersion();
            lblVersion.Text = $"{"Version"} {version.ToString(ExtendedVersionFormatFlags.BuildString | ExtendedVersionFormatFlags.CommitShort | ExtendedVersionFormatFlags.Truncated)}";
            lblVersion.LinkArea = version.Commit != null ? new LinkArea(lblVersion.Text.Length - version.Commit.ToShorthandString().Length, version.Commit.ToShorthandString().Length) : new LinkArea(0, 0);
        }

        private void UpdaterUtilities_UpdateResponse(object sender, UpdaterUtilities.UpdateResponseEventArgs e)
        {
            var showDialog = (bool) e.UserToken;

            if (e.Error == null)
            {
                var current = GetVersion().ToVersion();

                if (e.LatestVersion > current)
                {
                    var result = MessageBox.Show("A new update is available, would you like to download it?", "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                        Process.Start(e.Url.ToString());
                }

                else if (showDialog)
                {
                    MessageBox.Show("There are no updates available.", "No Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            else
            {
                Logging.GetFileLogger().Log(Level.Error, e.Error, suppressEvents: true);

                if (showDialog)
                    MessageBox.Show("There was an error while checking for updates.", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            btnUpdatecheck.Enabled = true;
        }

        private void CheckBackgroundTask()
        {
            _ignoreTaskSetting = true;

            chkBackgroundTask.Checked = TaskSchedulerUtilities.GetTask() != null;

            _ignoreTaskSetting = false;
        }

        private void OnLogEvent(object sender, LogExtensions.LogEventArgs e)
        {
            if (e.Log.Equals(Logging.GetFileLogger()))
                textBox1.AppendText($"[{DateTime.Now:T}] {e.Message}{Environment.NewLine}");
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            RefreshTelemetryServices(true);
            RefreshTelemetryTasks(true);
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshControls();
            textBox1.Clear();

            RefreshTelemetryServices(true);
            RefreshTelemetryTasks(true);
        }

        private void telemControl_CheckStateChanged(object sender, EventArgs e)
        {
            btnApply.Enabled = _tasksControl.CheckState == CheckState.Checked || _servicesControl.CheckState == CheckState.Checked;
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            if (_servicesControl.CheckState == CheckState.Checked)
                NvidiaController.DisableTelemetryServices(_telemetryServices, true, true);

            if (_tasksControl.CheckState == CheckState.Checked)
                NvidiaController.DisableTelemetryTasks(_telemetryTasks, true, true);

            RefreshControls();

            RefreshTelemetryServices(false);
            RefreshTelemetryTasks(false);
        }

        private void btnDefaults_Click(object sender, EventArgs e)
        {
            RefreshControls();

            NvidiaController.EnableTelemetryServices(_telemetryServices, true);
            NvidiaController.EnableTelemetryTasks(_telemetryTasks, true);

            RefreshTelemetryServices(false);
            RefreshTelemetryTasks(false);

            btnApply.Enabled = false;
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRefresh.Visible = btnDefaults.Visible = btnApply.Visible = tabControl1.SelectedTab == tabPage1;
        }

        private void RefreshControls()
        {
            _tasksControl.Reset();
            _servicesControl.Reset();
        }

        private void RefreshTelemetryTasks(bool logging)
        {
            var tasks = NvidiaController.GetTelemetryTasks(logging);

            foreach (var task in tasks)
            {
                _tasksControl.AddTelemetryItem(task, $"Task: {task.Task.Path}");
            }

            _tasksControl.Enabled = _tasksControl.TelemetryItems.Count != 0;
            _telemetryTasks = tasks.Select(t => t.Task).ToList();
        }

        private void RefreshTelemetryServices(bool logging)
        {
            var services = NvidiaController.GetTelemetryServices(logging);

            foreach (var service in services)
            {
                _servicesControl.AddTelemetryItem(service, $"Service: {service.Service.DisplayName}");
            }

            _servicesControl.Enabled = _servicesControl.TelemetryItems.Count != 0;
            _telemetryServices = services.Select(s => s.Service).ToList();
        }

        private void lblCopyright_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://nateshoffner.com");
        }

        private void pbGithub_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/NateShoffner/Disable-Nvidia-Telemetry");
        }

        private void lblGithub_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/NateShoffner/Disable-Nvidia-Telemetry");
        }

        private void pbDonate_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=nate.shoffner@gmail.com&lc=US&item_name=Disable%20Nvidia%20Telemetry&currency_code=USD&bn=PP%2dDonationsBF");
        }

        private void chkBackroundTask_CheckedChanged(object sender, EventArgs e)
        {
            cbTaskTrigger.Enabled = chkBackgroundTask.Checked;

            if (_ignoreTaskSetting)
                return;

            if (chkBackgroundTask.Checked)
                TaskSchedulerUtilities.Create((TaskSchedulerUtilities.TaskTrigger) Settings.Default.BackgroundTaskTrigger);
            else
                TaskSchedulerUtilities.Remove();
        }

        private void btnUpdatecheck_Click(object sender, EventArgs e)
        {
            btnUpdatecheck.Enabled = false;
            UpdaterUtilities.UpdateCheck(true);
        }

        private void chkUpdates_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.StartupUpdate = chkUpdates.Checked;
            Settings.Default.Save();
        }

        private void cbTaskTrigger_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Default.BackgroundTaskTrigger = cbTaskTrigger.SelectedIndex;
            Settings.Default.Save();
        }

        private void lblVersion_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start($"https://github.com/NateShoffner/Disable-Nvidia-Telemetry/commit/{GetVersion().Commit}");
        }

        private static ExtendedVersion.ExtendedVersion GetVersion()
        {
            var attribute =
                (AssemblyInformationalVersionAttribute) Assembly.GetExecutingAssembly()
                    .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false).FirstOrDefault();
            var version = attribute != null ? new ExtendedVersion.ExtendedVersion(attribute.InformationalVersion) : new ExtendedVersion.ExtendedVersion(Application.ProductVersion);

            return version;
        }

        private void chkFileLogging_CheckedChanged(object sender, EventArgs e)
        {
            Logging.Enabled = chkFileLogging.Checked;
            Settings.Default.FileLogging = chkFileLogging.Checked;
            Settings.Default.Save();
        }
    }
}