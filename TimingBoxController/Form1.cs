﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;

namespace TimingBoxController
{
    public partial class Form1 : Form
    {
        SerialPort ComPort = new SerialPort();
        delegate void SetTextCallback(string text);
        bool SerialPortOpen = false;

        public static class Variables
        {
            public static string logfilename;               // Logfile name
            public static System.IO.StreamWriter logfile;   // Logfile file ID
        }

        static class Constants
        {
            public const string SoftwareVersion = "1.3.2";
            public const int Baud = 9600;
            public const int InternalTrigger = 10;
            public const int ForceShutterOpen = 11;
            public const int ManualRx1 = 12;
            public const int Rx1Active = 13;
            public const int ManualRx2 = 14;
            public const int Rx2Active = 15;
            public const int Rate = 20;
            public const int InitialDelay = 21;
            public const int ShutterOpen = 22;
            public const int Camera = 23;
            public const int CameraDelay = 24;
            public const int ShutterClose = 25;
            public const int ImagingExposures = 26;
            public const int ImagingRepeats = 27;
            public const int ImagingFlats = 28;
            public const int Rx1Delay = 29;
            public const int Rx1Pulse = 30;
            public const int Rx1Repeats = 31;
            public const int Rx2Delay = 32;
            public const int Rx2Pulse = 33;
            public const int Rx2Repeats = 34;
            public const int ImagingStarts = 40;
            public const int Rx1Starts = 41;
            public const int Rx2Starts = 42;
            public const int ShutterMode = 45;
            public const int Search = 50;
            public const int OneShot = 51;
            public const int AcquireOne = 52;
            public const int Run = 53;
            public const int AcquireFlats = 54;
            public const int Stop = 55;
        }

        public Form1()
        {
            InitializeComponent();
            ComPort.BaudRate = Constants.Baud;
            ComPort.DataBits = 8;
            ComPort.Parity = Parity.None;
            ComPort.StopBits = StopBits.One;
            ComPort.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(port_DataReceived_1);
            LoadSettings();
            this.Text = "Timing Box Control v" + Constants.SoftwareVersion;
            Variables.logfile = new System.IO.StreamWriter(Variables.logfilename, append: true);
            Variables.logfile.WriteLine("-------------------------------------------------------");
            WriteLog("Timing hub version " + Constants.SoftwareVersion + " opened");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            WriteLog("Timing hub closed");
            Variables.logfile.Close();
            SaveSettings();
        }

        private void port_DataReceived_1(object sender, SerialDataReceivedEventArgs e)
        {
            int serialLength, serialParameter, serialValue;
            string InputData = ComPort.ReadLine();
            Console.WriteLine(InputData);
            ThreadHelperClass.SetText(this, labelRxCommand, Convert.ToString(InputData));
            parseFromString(InputData, out serialLength, out serialParameter, out serialValue);

            switch (serialParameter)
            {
                case 0:
                    // <0000 // connected
                    BeginInvoke(new EventHandler(delegate
                    {
                        SendAllSettings();
                    }));
                    WriteLog("Connected to timing hub");
                    MessageBox.Show(new Form { TopMost = true }, "Successfully connected to timing box");
                    break;
                case 55:
                    btnSearch.ForeColor = Color.Black;
                    btnOneShot.ForeColor = Color.Black;
                    btnAcquireOne.ForeColor = Color.Black;
                    btnRun.ForeColor = Color.Black;
                    btnAcquireFlats.ForeColor = Color.Black;
                    btnStop.ForeColor = Color.Red;
                    break;
            }
        }

        public void parseFromString(string input, out int serialLength, out int serialParameter, out int serialValue)
        {
            char[] delimiterChars = { '<', ',' , '\r' };
            var split = input.Split(delimiterChars);
            serialLength = int.Parse(split[1]);
            serialParameter = 0;
            serialValue = 0;

            switch (serialLength)
            {
                case 0:
                    serialParameter = 0;
                    serialValue = 0;
                    break;
                case 1:
                    serialParameter = int.Parse(split[2]);
                    serialValue = 0;
                    break;
                case 2:
                    serialParameter = int.Parse(split[2]);
                    serialValue = int.Parse(split[3]);
                    break;
                default:
                    break;
            }
        }

        public static class ThreadHelperClass
        {
            delegate void SetTextCallback(Form f, Control ctrl, string text);
            /// <summary>
            /// Set text property of various controls
            /// </summary>
            /// <param name="form">The calling form</param>
            /// <param name="ctrl"></param>
            /// <param name="text"></param>
            public static void SetText(Form form, Control ctrl, string text)
            {
                // InvokeRequired required compares the thread ID of the 
                // calling thread to the thread ID of the creating thread. 
                // If these threads are different, it returns true. 
                if (ctrl.InvokeRequired)
                {
                    SetTextCallback d = new SetTextCallback(SetText);
                    form.Invoke(d, new object[] { form, ctrl, text });
                }
                else
                {
                    ctrl.Text = text;
                }
            }
        }

        // --------------- BUTTONS ---------------

        private void btnGetSerialPorts_Click(object sender, EventArgs e)
        {
            cboPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports) cboPorts.Items.Add(port);
            cboPorts.Text = ports[0];
        }

        private void btnPortState_Click(object sender, EventArgs e)
        {
            if (SerialPortOpen)
            {
                btnPortState.Text = "Open";
                SerialPortOpen = false;
                ComPort.DiscardInBuffer();
                ComPort.Close();
                labelRxCommand.Text = "Serial port not open";
                WriteLog("Disconnected from timing hub");
            }
            else
            {
                btnPortState.Text = "Close";
                SerialPortOpen = true;
                ComPort.PortName = Convert.ToString(cboPorts.Text);
                ComPort.Open();
                ComPort.NewLine = "\n";

                // On SendCommand(0) the box responds with <0000 if connected correctly
                SendCommand(0);
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Reset();
            LoadSettings();

            // On SendCommand(0) the box responds with <0000 if connected correctly
            SendCommand(0);
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            SendCommand(Constants.Search);
            btnSearch.ForeColor = Color.Green;
            btnOneShot.ForeColor = Color.Black;
            btnAcquireOne.ForeColor = Color.Black;
            btnRun.ForeColor = Color.Black;
            btnAcquireFlats.ForeColor = Color.Black;
            btnStop.ForeColor = Color.Black;
            WriteLog("SEARCH");
        }

        private void btnOneShot_Click(object sender, EventArgs e)
        {
            SendCommand(Constants.OneShot);
            btnSearch.ForeColor = Color.Black;
            btnOneShot.ForeColor = Color.Green;
            btnAcquireOne.ForeColor = Color.Black;
            btnRun.ForeColor = Color.Black;
            btnAcquireFlats.ForeColor = Color.Black;
            btnStop.ForeColor = Color.Black;
            WriteLog("ONESHOT");
        }

        private void btnAcquireOne_Click(object sender, EventArgs e)
        {
            SendCommand(Constants.AcquireOne);
            btnSearch.ForeColor = Color.Black;
            btnOneShot.ForeColor = Color.Black;
            btnAcquireOne.ForeColor = Color.Green;
            btnRun.ForeColor = Color.Black;
            btnAcquireFlats.ForeColor = Color.Black;
            btnStop.ForeColor = Color.Black;
            WriteLog("ACQUIRE ONE BLOCK");
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            SendCommand(Constants.Run);
            btnSearch.ForeColor = Color.Black;
            btnOneShot.ForeColor = Color.Black;
            btnAcquireOne.ForeColor = Color.Black;
            btnRun.ForeColor = Color.Green;
            btnAcquireFlats.ForeColor = Color.Black;
            btnStop.ForeColor = Color.Black;
            WriteLog("RUN");
        }

        private void btnAcquireFlats_Click(object sender, EventArgs e)
        {
            SendCommand(Constants.AcquireFlats);
            btnSearch.ForeColor = Color.Black;
            btnOneShot.ForeColor = Color.Black;
            btnAcquireOne.ForeColor = Color.Black;
            btnRun.ForeColor = Color.Black;
            btnAcquireFlats.ForeColor = Color.Green;
            btnStop.ForeColor = Color.Black;
            WriteLog("FLAT/DARK");
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            SendCommand(Constants.Stop);
            btnSearch.ForeColor = Color.Black;
            btnOneShot.ForeColor = Color.Black;
            btnAcquireOne.ForeColor = Color.Black;
            btnRun.ForeColor = Color.Black;
            btnAcquireFlats.ForeColor = Color.Black;
            btnStop.ForeColor = Color.Red;
            WriteLog("STOP");
        }

        // --------------- CHECKBOXES ---------------

        private void checkBoxInternalTrigger_CheckedChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.InternalTrigger, Convert.ToDecimal(checkBoxInternalTrigger.Checked));
            if (checkBoxInternalTrigger.Checked) checkBoxInternalTrigger.ForeColor = Color.Red;
            else checkBoxInternalTrigger.ForeColor = Color.Black;
        }

        private void checkBoxShutterOpen_CheckedChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.ForceShutterOpen, Convert.ToDecimal(checkBoxShutterOpen.Checked));
            if (checkBoxShutterOpen.Checked)
            {
                checkBoxShutterOpen.ForeColor = Color.Red;
                WriteLog("Shutter OPEN");
            }
            else
            {
                checkBoxShutterOpen.ForeColor = Color.Black;
                WriteLog("Shutter normal");
            }
        }

        private void checkBoxManualRx1_CheckedChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.ManualRx1, Convert.ToDecimal(checkBoxManualRx1.Checked));
            if (checkBoxManualRx1.Checked)
            {
                checkBoxManualRx1.ForeColor = Color.Red;
                WriteLog("Rx1 ON");
            }
            else
            {
                checkBoxManualRx1.ForeColor = Color.Black;
                WriteLog("Rx1 OFF");
            }
        }

        private void checkBoxManualRx2_CheckedChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.ManualRx2, Convert.ToDecimal(checkBoxManualRx2.Checked));
            if (checkBoxManualRx2.Checked)
            {
                checkBoxManualRx2.ForeColor = Color.Red;
                WriteLog("Rx2 ON");
            }
            else
            {
                checkBoxManualRx2.ForeColor = Color.Black;
                WriteLog("Rx2 OFF");
            }
        }

        private void checkBoxRx1Active_CheckedChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.Rx1Active, Convert.ToDecimal(checkBoxRx1Active.Checked));
            if (checkBoxRx1Active.Checked) checkBoxRx1Active.ForeColor = Color.Black;
            else checkBoxRx1Active.ForeColor = Color.Red; 
        }

        private void checkBoxRx2Active_CheckedChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.Rx2Active, Convert.ToDecimal(checkBoxRx2Active.Checked));
            if (checkBoxRx2Active.Checked) checkBoxRx2Active.ForeColor = Color.Black;
            else checkBoxRx2Active.ForeColor = Color.Red;
        }

        // --------------- NUMERIC UP/DOWN BOXES ---------------

        private void numericUpDownRate_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.Rate, numericUpDownRate.Value);
            UpdateAcquireTime();
        }

        private void numericUpDownInitialDelay_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.InitialDelay, numericUpDownInitialDelay.Value);
            UpdateAcquireTime();
        }

        private void numericUpDownShutterOpen_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.ShutterOpen, numericUpDownShutterOpen.Value);
            UpdateAcquireTime();
        }

        private void numericUpDownCamera_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.Camera, numericUpDownCamera.Value);
            UpdateAcquireTime();
        }

        private void numericUpDownCameraDelay_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.CameraDelay, numericUpDownCameraDelay.Value);
            UpdateAcquireTime();
        }

        private void numericUpDownShutterClose_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.ShutterClose, numericUpDownShutterClose.Value);
            UpdateAcquireTime();
        }

        private void numericUpDownImagingExposures_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.ImagingExposures, numericUpDownImagingExposures.Value);
            UpdateAcquireTime();
        }

        private void numericUpDownImagingRepeats_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.ImagingRepeats, numericUpDownImagingRepeats.Value);
        }

        private void numericUpDownImagingFlats_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.ImagingFlats, numericUpDownImagingFlats.Value);
        }

        private void numericUpDownRx1Delay_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.Rx1Delay, numericUpDownRx1Delay.Value);
        }

        private void numericUpDownRx1Pulse_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.Rx1Pulse, numericUpDownRx1Pulse.Value);
        }

        private void numericUpDownRx1Repeats_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.Rx1Repeats, numericUpDownRx1Repeats.Value);
        }

        private void numericUpDownRx2Delay_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.Rx2Delay, numericUpDownRx2Delay.Value);
        }

        private void numericUpDownRx2Pulse_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.Rx2Pulse, numericUpDownRx2Pulse.Value);
        }

        private void numericUpDownRx2Repeats_ValueChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.Rx2Repeats, numericUpDownRx2Repeats.Value);
        }

        // --------------- TEXT BOXES ---------------

        private void textBoxImagingStarts_Leave(object sender, EventArgs e)
        {
            SendParameterList(Constants.ImagingStarts, textBoxImagingStarts.Text);
        }

        private void textBoxRx1Starts_Leave(object sender, EventArgs e)
        {
            SendParameterList(Constants.Rx1Starts, textBoxRx1Starts.Text);
        }

        private void textBoxRx2Starts_Leave(object sender, EventArgs e)
        {
            SendParameterList(Constants.Rx2Starts, textBoxRx2Starts.Text);
        }

        // --------------- COMBO BOXES ---------------

        private void comboBoxShutterMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            SendParameter(Constants.ShutterMode, comboBoxShutterMode.SelectedIndex);
            UpdateAcquireTime();
        }

        // --------------- DIALOG BOXES ---------------

        private void saveFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            Variables.logfilename = saveFileDialog1.FileName;
        }

        // --------------- USER FUNCTIONS ---------------

        public void SendCommand(int parameter)
        {
            string command = string.Format(">{0:0000},{1:0000}", 1, parameter);
            SerialCommand(command);
        }

        public void SendParameter(int parameter, decimal value)
        {
            string command = string.Format(">{0:0000},{1:0000},{2:0000}", 2, parameter, value);
            SerialCommand(command);
        }

        public void SendParameterList(int parameter, string times)
        {
            string[] imagingTimes = times.Split(',');
            for (int i = 0; i < imagingTimes.Length; i++) imagingTimes[i] = imagingTimes[i].PadLeft(4, '0');
            string command = string.Format(">{0:0000},{1:0000},{2:0000}", imagingTimes.Length + 1, parameter, string.Join(",", imagingTimes));
            SerialCommand(command);
        }

        public void SerialCommand(string command)
        {
            Console.WriteLine(command);
            labelTxCommand.Text = command;
            if (SerialPortOpen) ComPort.Write(command);

        }

        public void UpdateAcquireTime()
        {
            decimal acquire = 0;
            switch (comboBoxShutterMode.SelectedIndex)
            {
                case 0:
                    acquire =
                        numericUpDownInitialDelay.Value +
                        numericUpDownShutterOpen.Value +
                        numericUpDownCamera.Value * numericUpDownImagingExposures.Value +
                        numericUpDownCameraDelay.Value * (numericUpDownImagingExposures.Value - 1) +
                        numericUpDownShutterClose.Value;
                    break;
                case 1:
                    acquire =
                        numericUpDownInitialDelay.Value + (
                            numericUpDownShutterOpen.Value +
                            numericUpDownCamera.Value +
                            numericUpDownShutterClose.Value) * numericUpDownImagingExposures.Value +
                        numericUpDownCameraDelay.Value * (numericUpDownImagingExposures.Value - 1);
                    break;
                case 2:
                    acquire =
                        numericUpDownInitialDelay.Value +
                        numericUpDownCamera.Value * numericUpDownImagingExposures.Value +
                        numericUpDownCameraDelay.Value * (numericUpDownImagingExposures.Value - 1);
                    break;
            }
            labelAcquireTime.Text = acquire.ToString();

            if (acquire >= Convert.ToDecimal(numericUpDownRate.Value)) labelAcquireTime.ForeColor = Color.Red;
            else labelAcquireTime.ForeColor = Color.Black;
        }

        public void WriteLog(string button)
        {
            Variables.logfile.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff ") + button);

            if (new string[] { "SEARCH", "ONESHOT", "ACQUIRE ONE BLOCK", "RUN" }.Contains(button))
            {
                if (checkBoxInternalTrigger.Checked) Variables.logfile.WriteLine("- Internal timing period: " + numericUpDownRate.Value.ToString() + " ms");
                else Variables.logfile.WriteLine("- External trigger");
                if (checkBoxShutterOpen.Checked) Variables.logfile.WriteLine("- Shutter forced open");
                Variables.logfile.WriteLine("- Initial delay: " + numericUpDownInitialDelay.Value.ToString() + " ms");
                Variables.logfile.WriteLine("- Shutter open delay: " + numericUpDownShutterOpen.Value.ToString() + " ms");
                Variables.logfile.WriteLine("- Camera exposure length: " + numericUpDownCamera.Value.ToString() + " ms");
                Variables.logfile.WriteLine("- Shutter close delay: " + numericUpDownShutterClose.Value.ToString() + " ms");
            }

            if (new string[] { "ACQUIRE ONE BLOCK", "RUN" }.Contains(button))
            {
                switch (comboBoxShutterMode.SelectedIndex)
                {
                    case 0:
                        Variables.logfile.WriteLine("- Shutter mode: Breath");
                        break;
                    case 1:
                        Variables.logfile.WriteLine("- Shutter mode: Image");
                        break;
                    case 2:
                        Variables.logfile.WriteLine("- Shutter mode: Block");
                        break;
                }
                Variables.logfile.WriteLine("- Number of exposures per breath: " + numericUpDownImagingExposures.Value.ToString());
                if (numericUpDownImagingExposures.Value > 1) Variables.logfile.WriteLine("- Delay between exposures: " + numericUpDownCameraDelay.Value.ToString() + " ms");
                Variables.logfile.WriteLine("- Number of breaths per block: " + numericUpDownImagingRepeats.Value.ToString());
            }

            if (button == "RUN")
            {
                Variables.logfile.WriteLine("- Imaging start breath(s): " + textBoxImagingStarts.Text);
                if (checkBoxRx1Active.Checked)
                {
                    Variables.logfile.WriteLine("- Rx1 delay: " + numericUpDownRx1Delay.Value.ToString() + " ms");
                    Variables.logfile.WriteLine("- Rx1 pulse length: " + numericUpDownRx1Pulse.Value.ToString() + " ms");
                    Variables.logfile.WriteLine("- Number of breaths to repeat Rx1: " + numericUpDownRx1Repeats.Value.ToString());
                    Variables.logfile.WriteLine("- Rx1 start breath(s): " + textBoxRx1Starts.Text);
                }
                if (checkBoxRx2Active.Checked)
                {
                    Variables.logfile.WriteLine("- Rx2 delay " + numericUpDownRx2Delay.Value.ToString() + " ms");
                    Variables.logfile.WriteLine("- Rx2 pulse length " + numericUpDownRx2Pulse.Value.ToString() + " ms");
                    Variables.logfile.WriteLine("- Number of breaths to repeat Rx2: " + numericUpDownRx2Repeats.Value.ToString());
                    Variables.logfile.WriteLine("- Rx2 start breath(s): " + textBoxRx2Starts.Text);
                }
            }
        }

        public void LoadSettings()
        {
            // Load all settings
            checkBoxInternalTrigger.Checked = Properties.Settings.Default.InternalTrigger;
            checkBoxShutterOpen.Checked = Properties.Settings.Default.ForceShutterOpen;
            checkBoxRx1Active.Checked = Properties.Settings.Default.Rx1Active;
            checkBoxRx2Active.Checked = Properties.Settings.Default.Rx2Active;
            numericUpDownRate.Value = Properties.Settings.Default.Rate;
            numericUpDownInitialDelay.Value = Properties.Settings.Default.InitialDelay;
            numericUpDownShutterOpen.Value = Properties.Settings.Default.ShutterOpen;
            numericUpDownCamera.Value = Properties.Settings.Default.Camera;
            numericUpDownCameraDelay.Value = Properties.Settings.Default.CameraDelay;
            numericUpDownShutterClose.Value = Properties.Settings.Default.ShutterClose;
            numericUpDownImagingExposures.Value = Properties.Settings.Default.ImagingExposures;
            numericUpDownImagingRepeats.Value = Properties.Settings.Default.ImagingRepeats;
            numericUpDownImagingFlats.Value = Properties.Settings.Default.ImagingFlats;
            numericUpDownRx1Delay.Value = Properties.Settings.Default.Rx1Delay;
            numericUpDownRx1Pulse.Value = Properties.Settings.Default.Rx1Pulse;
            numericUpDownRx1Repeats.Value = Properties.Settings.Default.Rx1Repeats;
            numericUpDownRx2Delay.Value = Properties.Settings.Default.Rx2Delay;
            numericUpDownRx2Pulse.Value = Properties.Settings.Default.Rx2Pulse;
            numericUpDownRx2Repeats.Value = Properties.Settings.Default.Rx2Repeats;
            textBoxImagingStarts.Text = Properties.Settings.Default.ImagingStarts;
            textBoxRx1Starts.Text = Properties.Settings.Default.Rx1Starts;
            textBoxRx2Starts.Text = Properties.Settings.Default.Rx2Starts;
            comboBoxShutterMode.SelectedIndex = Properties.Settings.Default.ShutterMode;
            Variables.logfilename = Properties.Settings.Default.logfilename;
        }

        public void SaveSettings()
        {
            // Save all settings
            Properties.Settings.Default.InternalTrigger = checkBoxInternalTrigger.Checked;
            Properties.Settings.Default.ForceShutterOpen = checkBoxShutterOpen.Checked;
            Properties.Settings.Default.Rx1Active = checkBoxRx1Active.Checked;
            Properties.Settings.Default.Rx2Active = checkBoxRx2Active.Checked;
            Properties.Settings.Default.Rate = numericUpDownRate.Value;
            Properties.Settings.Default.InitialDelay = numericUpDownInitialDelay.Value;
            Properties.Settings.Default.ShutterOpen = numericUpDownShutterOpen.Value;
            Properties.Settings.Default.Camera = numericUpDownCamera.Value;
            Properties.Settings.Default.CameraDelay = numericUpDownCameraDelay.Value;
            Properties.Settings.Default.ShutterClose = numericUpDownShutterClose.Value;
            Properties.Settings.Default.ImagingExposures = numericUpDownImagingExposures.Value;
            Properties.Settings.Default.ImagingRepeats = numericUpDownImagingRepeats.Value;
            Properties.Settings.Default.ImagingFlats = numericUpDownImagingFlats.Value;
            Properties.Settings.Default.Rx1Delay = numericUpDownRx1Delay.Value;
            Properties.Settings.Default.Rx1Pulse = numericUpDownRx1Pulse.Value;
            Properties.Settings.Default.Rx1Repeats = numericUpDownRx1Repeats.Value;
            Properties.Settings.Default.Rx2Delay = numericUpDownRx2Delay.Value;
            Properties.Settings.Default.Rx2Pulse = numericUpDownRx2Pulse.Value;
            Properties.Settings.Default.Rx2Repeats = numericUpDownRx2Repeats.Value;
            Properties.Settings.Default.ImagingStarts = textBoxImagingStarts.Text;
            Properties.Settings.Default.Rx1Starts = textBoxRx1Starts.Text;
            Properties.Settings.Default.Rx2Starts = textBoxRx2Starts.Text;
            Properties.Settings.Default.ShutterMode = comboBoxShutterMode.SelectedIndex;
            Properties.Settings.Default.logfilename = Variables.logfilename;
            Properties.Settings.Default.Save();
        }

        public void SendAllSettings()
        {
            // Send all parameters to update timing box to match GUI
            SendParameter(Constants.InternalTrigger, Convert.ToDecimal(checkBoxInternalTrigger.Checked));
            SendParameter(Constants.ForceShutterOpen, Convert.ToDecimal(checkBoxShutterOpen.Checked));
            SendParameter(Constants.ManualRx1, Convert.ToDecimal(checkBoxManualRx1.Checked));
            SendParameter(Constants.Rx1Active, Convert.ToDecimal(checkBoxRx1Active.Checked));
            SendParameter(Constants.ManualRx2, Convert.ToDecimal(checkBoxManualRx2.Checked));
            SendParameter(Constants.Rx2Active, Convert.ToDecimal(checkBoxRx2Active.Checked));
            SendParameter(Constants.Rate, numericUpDownRate.Value);
            SendParameter(Constants.InitialDelay, numericUpDownInitialDelay.Value);
            SendParameter(Constants.ShutterOpen, numericUpDownShutterOpen.Value);
            SendParameter(Constants.Camera, numericUpDownCamera.Value);
            SendParameter(Constants.CameraDelay, numericUpDownCameraDelay.Value);
            SendParameter(Constants.ShutterClose, numericUpDownShutterClose.Value);
            SendParameter(Constants.ImagingExposures, numericUpDownImagingExposures.Value);
            SendParameter(Constants.ImagingRepeats, numericUpDownImagingRepeats.Value);
            SendParameter(Constants.ImagingFlats, numericUpDownImagingFlats.Value);
            SendParameter(Constants.Rx1Delay, numericUpDownRx1Delay.Value);
            SendParameter(Constants.Rx1Pulse, numericUpDownRx1Pulse.Value);
            SendParameter(Constants.Rx1Repeats, numericUpDownRx1Repeats.Value);
            SendParameter(Constants.Rx2Delay, numericUpDownRx2Delay.Value);
            SendParameter(Constants.Rx2Pulse, numericUpDownRx2Pulse.Value);
            SendParameter(Constants.Rx2Repeats, numericUpDownRx2Repeats.Value);
            SendParameterList(Constants.ImagingStarts, textBoxImagingStarts.Text);
            SendParameterList(Constants.Rx1Starts, textBoxRx1Starts.Text);
            SendParameterList(Constants.Rx2Starts, textBoxRx2Starts.Text);
            SendParameter(Constants.ShutterMode, comboBoxShutterMode.SelectedIndex);
        }
    }
}