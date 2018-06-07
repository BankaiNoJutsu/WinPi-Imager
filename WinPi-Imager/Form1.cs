using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.IO.Compression;

namespace WinPi_Imager
{
    public partial class Form1 : Form
    {
        #region External stuff
        internal const int SC_CLOSE = 0xF060;        // "X" (exit) button
        internal const int MF_ENABLED = 0x00000000;  // Enables targetItem
        internal const int MF_GRAYED = 0x1;          // Grays out the targetItem (?)
        internal const int MF_DISABLED = 0x00000002; // Disables targetItem

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr HWNDValue, bool isRevert);

        [DllImport("user32.dll")]
        private static extern int EnableMenuItem(IntPtr tMenu, int targetItem, int targetStatus);

        private IntPtr s_SystemMenuHandle;
        #endregion

        string[] args;

        private int[] diskIndexList = { 0 };
        private int step = 0;

        private int finalDiskIndex;
        private string finalRaspberryPiPkgPath;
        private string finalRpi3winStuffPath;

        private bool _noescape = false; // Set to true to disable ALT-F4

        public Form1(string[] args)
        {
            this.args = args;
            InitializeComponent();
            FormClosing += Form1_FormClosing;
            ComboBox.SelectedIndexChanged += ComboBox_SelectedIndexChanged;
        }

        private void ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
           // Used for testing if disk indexes match with diskpart.exe
            // MessageBox.Show("ComboBox index: " + ComboBox.SelectedIndex +
            //     "\nDisk index: " + diskIndexList[ComboBox.SelectedIndex]);
        }

        private void DisableExitFuntions()
        {
            _noescape = true;
            s_SystemMenuHandle = GetSystemMenu(this.Handle, false);
            EnableMenuItem(s_SystemMenuHandle, SC_CLOSE, MF_DISABLED);
        }

        private void EnableExitFunctions()
        {
            _noescape = false;
            EnableMenuItem(s_SystemMenuHandle, SC_CLOSE, MF_ENABLED);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_noescape) e.Cancel = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Icon = Properties.Resources.RaspiWin_Icon;
            TextBox.BackColor = System.Drawing.SystemColors.Window;
            PathBox.BackColor = System.Drawing.SystemColors.Window;
            TextBox.Lines = new string[]{ "Welcome to Windows on Raspberry Pi Imager! This program will image a modified version of Windows on ARM64 to an SD card for use with a Raspberry Pi 3B or 3B+." +
                           " You will need:", "",
                           "- An SD card with at least 16GB capacity",
                           "- A Raspberry Pi 3B or 3B+",
                           "- A Windows 10 Arm64 Professional 17134.1 Insider ISO",
                           "- Time and patience","",
                           "Credits:",
                           "andreiw for implementing the bootloader/UEFI for Raspberry Pi",
                           "UEFI Github: https://github.com/andreiw/RaspberryPiPkg","",
                           "Ash-Bash for the idea",
                           "Github: https://github.com/Ash-Bash","",
                           "WARNINGS:",
                           "- Imaging Windows to your SD card will take at least an hour.",
                           "- This is NOT Windows 10 IOT. It's a complete copy of Windows 10 on ARM64 with a desktop and *.exe support.",
                           "- Drivers for the Raspberry Pi are still in development. Currently used USB driver isn't that great, it can randomly stop while using it. Currently used SD card driver is " +
                           "slow as high speed is not implemented, but it works with no issues. There are no drivers for anything other than USB and SD card.",
                           "- This tool is still in development as well. If you want to help developing this tool, be sure to check out the repository on Github." };
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (CheckBox.Text == "I read the message")
            {
                CheckBox.Checked = false;
                CheckBox.Text = "I really read the message";
            }
            else NextButton.Enabled = CheckBox.Checked;
        }

        private void TextBoxWriteLine(string text) => 
            Invoke((MethodInvoker)delegate { TextBox.AppendText(Environment.NewLine + text); });
        
        private Process RunDiskpart(string[] commands, string extraArguments = "")
        {
            File.WriteAllLines("diskpart.txt", commands);
            Thread.Sleep(1000);
            return Process.Start(new ProcessStartInfo
            {
                FileName = "diskpart.exe",
                Arguments = "/s diskpart.txt " + extraArguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            });
        }


        // EFI: Q:\
        // Windows: T:\
        private void ImageToDisk()
        {
            string output;
            Process proc = RunDiskpart(new string[]{
                    "sel disk " + finalDiskIndex,
                    "clean",
                    "create partition primary size=200",
                    "format fs=fat32 quick",
                    "assign letter=Q",
                    "create partition primary",
                    "format fs=ntfs quick",
                    "assign letter=T"
                });
            TextBoxWriteLine("Partitioning SD card with diskpart.exe...");
            output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            TextBoxWriteLine(output);
            if (proc.ExitCode != 0)
            {
                MessageBox.Show("Unable to partition the SD card. Are you running the latest version of Windows 10? If not, please update your computer to the latest version of Windows 10. If you are running the latest version, please try again."
                    , "Partitioning Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }
            ProgressBar.Value = 4;
            Thread.Sleep(500);
            ProgressBar.Value = 5;
            
        }

        private void NextButton_Click(object sender, EventArgs e)
        {
            step++;
            ChangeStep();
        }

        private void ChangeStep()
        {
            if (step == 1)
            {
                TextBox.Visible = false;
                TextBox.Enabled = false;
                ComboBox.Enabled = true;
                ComboBox.Visible = true;
                CheckBox.Visible = false;
                CheckBox.Enabled = false;
                RefreshDisksButton.Enabled = true;
                RefreshDisksButton.Visible = true;
                ContentLabel.Text = "Please select your SD card. All the data on the SD card will be destroyed and replaced\n" +
                                    "with Windows 10 ARM64 image.";
                RefreshDisks();
                TitleLabel.Text = "Select SD Card";
                NextButton.Enabled = true;
            }
            else if (step == 2)
            {
                finalDiskIndex = diskIndexList[ComboBox.SelectedIndex];
                // MessageBox.Show(finalDiskIndex.ToString());
                ComboBox.Visible = false;
                ComboBox.Enabled = false;
                PathBox.Visible = true;
                PathBox.Enabled = true;
                TitleLabel.Text = "Select RaspberryPiPkg-master.zip";
                NextButton.Enabled = false;
                ContentLabel.Text = "Please select \"RaspberryPiPkg-master.zip\" file. This file can be downloaded from\n" +
                                    "Github.";
                RefreshDisksButton.Text = "Browse";
                // DownloadButton.Visible = true;
                // DownloadButton.Enabled = true;
                VisitLinkLabel.Visible = true;
                VisitLinkLabel.Enabled = true;
            }
            else if (step == 3)
            {
                finalRaspberryPiPkgPath = PathBox.Text;
                // MessageBox.Show(finalRaspberryPiPkgPath);
                TitleLabel.Text = "Select rpi3winstuff-master.zip";
                ContentLabel.Text = "Please select \"rpi3winstuff-master.zip\" file. This file can be downloaded from\n" +
                                    "Github as well.";
                PathBox.Text = "";
                NextButton.Enabled = false;
            }
            else if (step == 4)
            {
                finalRpi3winStuffPath = PathBox.Text;
                PathBox.Enabled = false;
                PathBox.Visible = false;
                RefreshDisksButton.Enabled = false;
                RefreshDisksButton.Visible = false;
                VisitLinkLabel.Enabled = false;
                VisitLinkLabel.Visible = false;
                ContentLabel.Text = "Press OK if the following information is correct. If not, please restart the application.";
                TextBox.Lines = new string[]
                {
                    "Target disk's index: " + finalDiskIndex,
                    "Path to \"RaspberryPiPkg-master.zip\": " + finalRaspberryPiPkgPath,
                    "Path to \"rpi3winstuff-master.zip\": " + finalRpi3winStuffPath,
                    "Path to Windows Arm64 ISO: " + "FUNCTIONALITY NOT IMPLEMENTED",
                    "",
                    "OTHER INFORMATION",
                    "Your ISO has to match this information:",
                    "Operating System: Windows 10 Arm64 Professional",
                    "Operating System Version: 17134.1 (recommended) or 17672 (Insider Preview)"
                };
                TextBox.Visible = true;
                TextBox.Enabled = true;
                NextButton.Text = "OK";
                TitleLabel.Text = "Summary";
            }
            else if (step == 5)
            {
                TextBox.ForeColor = Color.LightGreen;
                TextBox.BackColor = Color.Black;
                TextBox.Text = "";
                TextBox.Font = new Font("Consolas", TextBox.Font.Size);
                TextBox.Text = "Beginning the process..." + Environment.NewLine;
                NextButton.Text = "Next";
                NextButton.Enabled = false;
                NextButton.Visible = false;
                ProgressBar.Visible = true;
                ProgressBar.Enabled = true;
                DisableExitFuntions();
                Task.Run(() => ImageToDisk());
            }
            else
            {
                MessageBox.Show("The next step is not implemented. The program cannot continue.", "Missing step", MessageBoxButtons.OK, MessageBoxIcon.Error);
                EnableExitFunctions();
                Application.Exit();
            }
        }

        private void RefreshDisks()
        {
            ComboBox.Items.Clear();
            ManagementObjectSearcher win32DiskDrives = new ManagementObjectSearcher("select * from Win32_DiskDrive");
            diskIndexList = new int[win32DiskDrives.Get().Count];
            int i = 0;
            foreach (ManagementObject win32DiskDrive in win32DiskDrives.Get())
            {
                Int64 size;
                int index = Convert.ToInt32(win32DiskDrive.Properties["Index"].Value);
                string model = win32DiskDrive.Properties["Model"].Value.ToString();
                string mediaType;
                if (win32DiskDrive.Properties["Size"].Value != null)
                {
                    string sizeString = win32DiskDrive.Properties["Size"].Value.ToString();
                    size = Int64.Parse(sizeString) / 1024 / 1024 / 1024;
                }
                else
                {
                    size = 0;
                }

                if (win32DiskDrive.Properties["MediaType"].Value != null)
                {
                    mediaType = win32DiskDrive.Properties["MediaType"].Value.ToString();
                }
                else
                {
                    mediaType = "Unknown Media Type";
                }
                diskIndexList[i] = index;
                i++;
                ComboBox.Items.Add("Disk " + index + " - " + model + " - " + mediaType + " - " + size.ToString() + "GB");
            }

            if (ComboBox.Items.Count > 0)
            {
                ComboBox.SelectedIndex = 0;
            }
        }


        private void RefreshDisksButton_Click(object sender, EventArgs e) // Used as Browse button as well
        {
            if (step == 1) RefreshDisks();
            else
            {
                string filter = "All files|*.*";
                if (step == 2) filter = "RaspberryPiPkg-master.zip|RaspberryPiPkg-master.zip";
                else if (step == 3) filter = "rpi3winstuff-master.zip|rpi3winstuff-master.zip";
                else return;
                OpenFileDialog dialog = new OpenFileDialog
                {
                    Filter = filter,
                    RestoreDirectory = true
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    PathBox.Text = dialog.FileName;
                    NextButton.Enabled = true;
                }
            }
        }

        private void DownloadButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Not implemented yet. Pressing OK will open the RaspberryPiPkg Github" +
                            " project. Press \"Clone or download\", then press \"Download ZIP\"" +
                            " to download RaspberryPiPkg-master.zip. You can then choose that file" +
                            " with the browse button.", "Not implemented", MessageBoxButtons.OK,
                             MessageBoxIcon.Warning);
            Process.Start("https://github.com/andreiw/RaspberryPiPkg");
        }

        private void VisitLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (step == 2)
            {
                Process.Start("https://github.com/andreiw/RaspberryPiPkg");
            }
            else if (step == 3)
            {
                Process.Start("https://github.com/andreiw/rpi3winstuff");
            }
        }
    }
}
