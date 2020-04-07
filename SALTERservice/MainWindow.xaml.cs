using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using System.Timers;
using System.Text.RegularExpressions;

namespace SALTERservice
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            //retrieves surveorInfo stored by sample manager
            initialiseSurveyorInfo();

            //This timer lets us retrieve the absolute final value. It needs to be set here in order for global varibales to act accordingly, otherwise
            //they are cleared after the final result data-check.
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);

            //starts looking for Salter BT device
            StartBleDeviceWatcher();

        }

        //Update UI to display connection status
        public void updateConnectionStatus(string text)
        {
            if (text == "CONNECTED")
            {
                Application.Current.Dispatcher.Invoke(() => { Connectionstatus.Text = text; Connectionstatus.Foreground = Brushes.Green; });
            }
            if (text == "Disconnected")
            {
                Application.Current.Dispatcher.Invoke(() => { Connectionstatus.Text = text; Connectionstatus.Foreground = Brushes.Black; });
            }
            
        }

        //For use in obtaining measurements from manual entry
        public void SetW1Measurement(string text)
        {
            Application.Current.Dispatcher.Invoke(() => { W1Measurement.Text = text; });
        }

        public void SetW2Measurement(string text)
        {
            Application.Current.Dispatcher.Invoke(() => { W2Measurement.Text = text; });
        }

        public void SetW3Measurement(string text)
        {
            Application.Current.Dispatcher.Invoke(() => { W3Measurement.Text = text; });
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            //CSV conversion must go here with appropriate handling. Currently checking for decimal point in measurement
            decimal measurement1;
            decimal measurement2;
            try
            {
                if (arrayMeasurements[1, 1].Contains(".") && check1DecimalPlace(arrayMeasurements[1, 1]) == true && arrayMeasurements[2, 1].Contains(".") && check1DecimalPlace(arrayMeasurements[2, 1]) == true)//Checking for decimal point existing. This could be improved?
                {
                    arrayMeasurements[1, 6] = "BluetoothInput";//Set as BluetoothInput for logging's sake
                    arrayMeasurements[2, 6] = "BluetoothInput";
                    measurement1 = ConvertStrToDec(arrayMeasurements[1, 1]);
                    measurement2 = ConvertStrToDec(arrayMeasurements[2, 1]);
                    if (CheckGreaterOnePercentDiff(measurement1, measurement2) == false)//Checking that there is a less than 1% difference between two measurements
                    {
                        string csv = ArrayToCsv(arrayMeasurements);
                        WriteCSVFile(csv);
                        Application.Current.Shutdown();
                    }
                    else
                    {
                        //Input has a greater than 1% difference therefore a third measurement is required.
                        isThirdMeasurement = true;
                        waiting3rdMeasurement.Visibility = Visibility.Visible;
                        W1Measurement.IsEnabled = false;
                        W2Measurement.IsEnabled = false;
                        W1Measurement_TextBox.IsEnabled = false;
                        W2Measurement_TextBox.IsEnabled = false;
                        clear1.IsEnabled = false;
                        clear2.IsEnabled = false;
                        checkBox.IsEnabled = false;
                        MessageBox.Show("Third measurement required.\n\nPlease take 5 seconds for respondent to re-position themselves for re-taking measurement.\n\n" +
                        "3rd measurement will be enabled after 5 seconds of closing this message.");
                        waiting3rdMeasurement.Visibility = Visibility.Hidden;
                        Thread.Sleep(5000);
                        checkBox.IsEnabled = true;
                        clear1.IsEnabled = false;
                        clear2.IsEnabled = false;
                        textBlock4.Visibility = Visibility.Visible;
                        textBlock5.Visibility = Visibility.Visible;
                        clear3.Visibility = Visibility.Visible;
                        textBlock6_Copy1.Visibility = Visibility.Visible;
                        W3Measurement.Visibility = Visibility.Visible;
                        button.Visibility = Visibility.Hidden;
                        button.IsEnabled = false;
                        button1.Visibility = Visibility.Visible;
                        button1.IsEnabled = true;

                    }
                }
                else
                {
                    //A decimal point is not present. BT transmission always sends a decimal point.
                    MessageBox.Show("Incorrect weight format. \n\nPlease ensure you've collected results using Salter Scales.\n\n" +
                        "If entering manually, ensure the measurement is exactly what is shown on scales.\n\n" +
                        "The measurement expected is 1 decimal place. For example 70 kg must be input as 70.0");
                }
            }
            catch
            {   //array is indexed in appropriately, therefore null measurement or uncorrect measurement format
                MessageBox.Show("Please enter some measurements and ensure you've collected results using Salter Scales.\n\n" +
                   "If entering manually, ensure the measurement is exactly what is shown on scales.\n\n" +
                       "The measurement expected is 1 decimal place. For example 70 kg must be input as 70.0");
            }
        }

        //This button is for the third measurement for a BT input.
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string[] respondentInfo = GetRespondentIdentifiers();
                arrayMeasurements[3, 2] = respondentInfo[0];
                arrayMeasurements[3, 3] = respondentInfo[1];
                arrayMeasurements[3, 4] = respondentInfo[2];
                arrayMeasurements[3, 5] = respondentInfo[3];
                if (arrayMeasurements[3, 1].Contains(".") && check1DecimalPlace(arrayMeasurements[3, 1]) == true)//Checking for decimal point existing
                {
                        arrayMeasurements[3, 6] = "BluetoothInput"; 
                        string csv = ArrayToCsv(arrayMeasurements);
                        WriteCSVFile(csv);
                        Application.Current.Shutdown();
                }
                else
                {   //A decimal point is not present. BT transmission always sends a decimal point.
                    MessageBox.Show("Incorrect weight format. \n\nPlease ensure you've collected results using Salter Scales.\n\n" +
                        "If entering manually, ensure the measurement is exactly what is shown on scales.\n\n" +
                        "The measurement expected is 1 decimal place. For example 70 kg must be input as 70.0");
                }
            }
            catch
            {   //array is indexed in appropriately, therefore null measurement or uncorrect measurement format
                MessageBox.Show("Please enter some measurements and ensure you've collected results using Salter Scales.\n\n" +
                    "If entering manually, ensure the measurement is exactly what is shown on scales.\n\n" +
                        "The measurement expected is 1 decimal place. For example 70 kg must be input as 70.0");
            }
        }

        //This button is for the first attempt at logging two manual measurements.
        private void button2_Click(object sender, RoutedEventArgs e)
        {
            //In the case of button3 click, manualmeasurement == true. So all existing string input must be converted to decimal and added appropriately to arrayMeasurements.
            //Once added to arrayMeasurements, run the necessary checks in the exact same manner that BT measurements are checked. If greater than 1% diff then button4
            //must be enabled which allows submission of third manual measurement. Button3 and Button4 must be disabled upon manualMeasurement unchecked i.e. manualmeasurement == false
            decimal measurement1;
            decimal measurement2;

            try
            {
                //Set arrayMeasurements to equal what surveyor has put manually into text fields. Set Manual Input
                arrayMeasurements[1, 0] = "WT";
                arrayMeasurements[1, 1] = W1Measurement_TextBox.Text;
                arrayMeasurements[2, 0] = "WT";
                arrayMeasurements[2, 1] = W2Measurement_TextBox.Text;
                arrayMeasurements[1, 6] = "ManualInput";
                arrayMeasurements[2, 6] = "ManualInput";
                char check1dp1stmeasurement = arrayMeasurements[1, 1][arrayMeasurements[1, 1].IndexOf(".") + 1];

                //Checking for decimal point for all weight possibilities. Using same verification as BT measurement. 
                if (arrayMeasurements[1, 1].Contains(".") && check1DecimalPlace(arrayMeasurements[1, 1]) == true && arrayMeasurements[2, 1].Contains(".") && check1DecimalPlace(arrayMeasurements[2, 1]) == true)
                {
                    measurement1 = ConvertStrToDec(arrayMeasurements[1, 1]);
                    measurement2 = ConvertStrToDec(arrayMeasurements[2, 1]);
                    if (CheckGreaterOnePercentDiff(measurement1, measurement2) == false)//Checking that there is a less than 1% difference between two measurements
                    {
                        string csv = ArrayToCsv(arrayMeasurements);
                        WriteCSVFile(csv);
                        Application.Current.Shutdown();
                    }
                    else
                    {
                        //Disable first two measurement boxes. Enable third measurement box, shift focus to third measurement, disable Done measuring Box, 
                        //enable submit final measurements.
                        W1Measurement.IsEnabled = false;
                        W2Measurement.IsEnabled = false;
                        W1Measurement_TextBox.IsEnabled = false;
                        W2Measurement_TextBox.IsEnabled = false;
                        clear1.IsEnabled = false;
                        clear2.IsEnabled = false;
                        button.IsEnabled = false;
                        button.Visibility = Visibility.Hidden;
                        button1.IsEnabled = false;
                        button1.Visibility = Visibility.Hidden;
                        button2.IsEnabled = false;
                        button2.Visibility = Visibility.Hidden;
                        waiting3rdMeasurement.Visibility = Visibility.Visible;
                        checkBox.IsEnabled = false;
                        MessageBox.Show("Third measurement required.\n\nPlease take 5 seconds for respondent to re-position themselves for re-taking measurement.\n\n" +
                        "3rd measurement will be enabled after 5 seconds of closing this message.");
                        Thread.Sleep(5000);
                        checkBox.IsEnabled = true;
                        waiting3rdMeasurement.Visibility = Visibility.Hidden;
                        textBlock6.Visibility = Visibility.Visible;
                        textBlock5.Visibility = Visibility.Visible;
                        W3Measurement_TextBox.Visibility = Visibility.Visible;
                        clear3.Visibility = Visibility.Visible;
                        button3.Visibility = Visibility.Visible;
                        button3.IsEnabled = true;
                        textBlock6_Copy1.Visibility = Visibility.Visible;                        
                        W3Measurement.Visibility = Visibility.Hidden;
                        W3Measurement_TextBox.Visibility = Visibility.Visible;
                        W3Measurement_TextBox.Focus();
                        W3Measurement.IsEnabled = false;
                        W3Measurement_TextBox.IsEnabled = true;
                        textBlock4.Visibility = Visibility.Visible;
                        textBlock5.Visibility = Visibility.Visible;                        
                        clear3.Visibility = Visibility.Visible;
                    }

                }
                else
                {
                    MessageBox.Show("Incorrect weight format. \n\nPlease ensure you've collected results using Salter Scales.\n\n" +
                        "If entering manually, ensure the measurement is exactly what is shown on scales.\n\n" +
                        "The measurement expected is 1 decimal place. For example 70 kg must be input as 70.0");
                }
            }
            catch
            {   //array indexing exception, user has entered either no data or some invalid data.
                MessageBox.Show("Please enter some measurements and ensure you've collected results using Salter Scales.\n\n" +
                   "If entering manually, ensure the measurement is exactly what is shown on scales.\n\n" +
                       "The measurement expected is 1 decimal place. For example 70 kg must be input as 70.0");
            }
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string[] respondentInfo = GetRespondentIdentifiers();
                arrayMeasurements[3, 2] = respondentInfo[0];
                arrayMeasurements[3, 3] = respondentInfo[1];
                arrayMeasurements[3, 4] = respondentInfo[2];
                arrayMeasurements[3, 5] = respondentInfo[3];

                //Set measurements to be obtained from manual entry and set manual input type
                arrayMeasurements[3, 0] = "WT";
                arrayMeasurements[3, 1] = W3Measurement_TextBox.Text;
                arrayMeasurements[3, 6] = "ManualInput";

                if (arrayMeasurements[3, 1].Contains(".") && check1DecimalPlace(arrayMeasurements[3, 1]) == true)//Check for decimal place existing
                {
                    string csv = ArrayToCsv(arrayMeasurements);
                    WriteCSVFile(csv);
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show("Incorrect weight format.\n\nPlease ensure you've collected results using Salter Scales.\n\n" +
                        "If entering manually, ensure the measurement is exactly what is shown on scales.\n\n" +
                        "The measurement expected is 1 decimal place. For example 70 kg must be input as 70.0");
                }
            }
            catch
            {
                MessageBox.Show("Please enter some measurements and ensure you've collected results using Salter Scales.\n\n" +
                   "If entering manually, ensure the measurement is exactly what is shown on scales.\n\n" +
                       "The measurement expected is 1 decimal place. For example 70 kg must be input as 70.0");
            }
        }


        bool manualMeasurement = false;
        bool regexOverride = false;//allows usage of text box clear operations to delte old results by not having regex applied to user input
        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {
            clear1.IsEnabled = true;
            clear2.IsEnabled = true;
            regexOverride = true;
            manualMeasurement = true;
            Application.Current.Dispatcher.Invoke(() => { W1Measurement_TextBox.Clear(); W2Measurement_TextBox.Clear(); W3Measurement_TextBox.Clear(); });
            MessageBox.Show("You are now entering measurements manually.\n\n" +
                "Please ensure measurements are of 1 decimal place format\n\n" +
                "For example, 80 kg should be inout as 80.0\n" +
                "140 kg should be input as 140.0");
            //////
            RunCleanUp();
            W1Measurement_TextBox.Focus();
            ///////
            regexOverride = false;
        }

        private void checkBox_Unchecked(object sender, RoutedEventArgs e)
        {
            allMeasurements.Clear();
            initialiseSurveyorInfo(); //re-initialise array for a return to BT input.
            W1Measurement.Text = "-Empty-";
            W2Measurement.Text = "-Empty-";
            clear1.IsEnabled = true;
            clear2.IsEnabled = true;
            regexOverride = true;
            manualMeasurement = false;
            Application.Current.Dispatcher.Invoke(() => { W1Measurement_TextBox.Clear(); W2Measurement_TextBox.Clear(); W3Measurement_TextBox.Clear(); });
            MessageBox.Show("You are now entering measurements with Bluetooth.");
            //////
            RunCleanUp();
            ////////
            regexOverride = false;
        }

        //Clearing measurements from indivudal fields
        private void clear1_Click(object sender, RoutedEventArgs e)
        {
            clearWasclicked = true; //setting this to true allows us to override the set up warnings when clearing first BT measurement. It must be set to false where measurement added to array.
            regexOverride = true;
            arrayMeasurements[1, 0] = null;
            arrayMeasurements[1, 1] = null;
            if (manualMeasurement == true)
            {
                
                Application.Current.Dispatcher.Invoke(() => { W1Measurement_TextBox.Clear(); });
                W1Measurement_TextBox.Focus();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { W1Measurement.Text = "-Empty-"; });
                allMeasurements.Clear();
            }
            regexOverride = false;
        }

        private void clear2_Click(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            if (manualMeasurement == true)
            {
                Application.Current.Dispatcher.Invoke(() => { W2Measurement_TextBox.Clear(); });
                W2Measurement_TextBox.Focus();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { W2Measurement.Text = "-Empty-"; });
                allMeasurements.Clear();
                string[] temp = { "temp", "temp" };
                allMeasurements.Add(temp); //allows a temp value to be added to allmeasurements so secodn value re-fires. second value only added if allmeasurements.count == 2
            }
            regexOverride = false;
        }

        private void clear3_Click(object sender, RoutedEventArgs e)
        {
            regexOverride = true;
            if (manualMeasurement == true)
            {
                Application.Current.Dispatcher.Invoke(() => { W3Measurement_TextBox.Clear(); });
                W3Measurement_TextBox.Focus();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => { W3Measurement.Text = "-Empty-"; });
            }
            regexOverride = false;
        }

        //This is run when setting manualMeasurement on or off via checkbox. Clears all fields and re-sets for taking 1st measurement.
        public void RunCleanUp()
        {
            //reset all measruements
            arrayMeasurements[1, 1] = null;
            arrayMeasurements[2, 1] = null;
            arrayMeasurements[3, 0] = null;
            arrayMeasurements[3, 1] = null;
            arrayMeasurements[1, 6] = null;
            arrayMeasurements[2, 6] = null;
            arrayMeasurements[3, 6] = null;
            arrayMeasurements[3, 2] = null;
            arrayMeasurements[3, 3] = null;
            arrayMeasurements[3, 4] = null;
            arrayMeasurements[3, 5] = null;
            allMeasurements.Clear();

            //enable first 2 measurement fields
            

            if (manualMeasurement == true) //Enable the manualMeasurement == true button to perform submission calcs using the manually entered measurements and not timer entered measurements.
            {
                W1Measurement.IsEnabled = false;
                W2Measurement.IsEnabled = false;
                W1Measurement.Visibility = Visibility.Hidden;
                W2Measurement.Visibility = Visibility.Hidden;
                W1Measurement_TextBox.IsEnabled = true;
                W2Measurement_TextBox.IsEnabled = true;
                W1Measurement_TextBox.Visibility = Visibility.Visible;
                W2Measurement_TextBox.Visibility = Visibility.Visible;
                button.IsEnabled = false;
                button.Visibility = Visibility.Hidden;
                button2.IsEnabled = true;
                button2.Visibility = Visibility.Visible;
                W1Measurement_TextBox.IsEnabled = true;
                W2Measurement_TextBox.IsEnabled = true;
            }
            else //Bluetooth measuring so setting initial button again.
            {
                button.IsEnabled = true;
                button.Visibility = Visibility.Visible;
                W1Measurement.IsEnabled = true;
                W2Measurement.IsEnabled = true;
                W1Measurement.Visibility = Visibility.Visible;
                W2Measurement.Visibility = Visibility.Visible;
                W1Measurement_TextBox.IsEnabled = false;
                W2Measurement_TextBox.IsEnabled = false;
                W1Measurement_TextBox.Visibility = Visibility.Hidden;
                W2Measurement_TextBox.Visibility = Visibility.Hidden;
                button2.IsEnabled = false;
                button2.Visibility = Visibility.Hidden;
                button3.IsEnabled = false;
                button3.Visibility = Visibility.Hidden;

            }

            //clear visibility of all things related to taking the third measurement
            textBlock6_Copy1.Visibility = Visibility.Hidden;
            W3Measurement.Visibility = Visibility.Hidden;
            W3Measurement_TextBox.Visibility = Visibility.Hidden;
            W3Measurement.IsEnabled = false;
            W3Measurement_TextBox.IsEnabled = false;
            button1.Visibility = Visibility.Hidden;
            textBlock4.Visibility = Visibility.Hidden;
            textBlock5.Visibility = Visibility.Hidden;
            clear3.Visibility = Visibility.Hidden;
          

            //Previous input used in Regex expressions for only allowing certain char input. Clearing these avoids duplication of previous inout values.
            previousInput = "";
            previousInput1 = "";
            previousInput2 = "";
        }

  
        #region DeviceDiscovery

        private ObservableCollection<BluetoothLEDeviceDisplay> KnownDevices = new ObservableCollection<BluetoothLEDeviceDisplay>();
        private List<DeviceInformation> UnknownDevices = new List<DeviceInformation>();

        private DeviceWatcher deviceWatcher;
        private void StartBleDeviceWatcher()
        {
            // Additional properties we would like about the device.
            // Property strings are documented here https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };

            // BT_Code: Example showing paired and non-paired in a single query.
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

            deviceWatcher =
                    DeviceInformation.CreateWatcher(
                        aqsAllBluetoothLEDevices,
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            //deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            //deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start over with an empty collection.
            KnownDevices.Clear();

            // Start the watcher. Active enumeration is limited to approximately 30 seconds.
            // This limits power usage and reduces interference with other Bluetooth activities.
            // To monitor for the presence of Bluetooth LE devices for an extended period,
            // use the BluetoothLEAdvertisementWatcher runtime class. See the BluetoothAdvertisement
            // sample for an example.
            deviceWatcher.Start();
        }

        /// <summary>
        /// Stops watching for all nearby Bluetooth devices.
        /// </summary>
        private void StopBleDeviceWatcher()
        {
            if (deviceWatcher != null)
            {
                // Unregister the event handlers.
                deviceWatcher.Added -= DeviceWatcher_Added;
                deviceWatcher.Updated -= DeviceWatcher_Updated;
                //deviceWatcher.Removed -= DeviceWatcher_Removed;
                deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
                //deviceWatcher.Stopped -= DeviceWatcher_Stopped;

                // Stop the watcher.
                deviceWatcher.Stop();
                deviceWatcher = null;
            }
        }

        private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in KnownDevices)
            {
                if (bleDeviceDisplay.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }

        private DeviceInformation FindUnknownDevices(string id)
        {
            foreach (DeviceInformation bleDeviceInfo in UnknownDevices)
            {
                if (bleDeviceInfo.Id == id)
                {
                    return bleDeviceInfo;
                }
            }
            return null;
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            await Task.Run(async () =>
            {
                lock (this)
                {
                    

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        // Make sure device isn't already present in the list.
                        if (FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                        {
                            if (deviceInfo.Name != string.Empty)
                            {
                                // If device has a friendly name display it immediately.
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                            }
                            else
                            {
                                // Add it to a list in case the name gets updated later. 
                                UnknownDevices.Add(deviceInfo);
                            }
                        }

                    }
                }
            });

        }

        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {

            //if contains salter and salter is connectable stop all other handlers and connect  
            await Task.Run(async () =>
            {
                lock (this)
                {
                   

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                        if (bleDeviceDisplay != null)
                        {
                            // Device is already being displayed - update UX.
                            bleDeviceDisplay.Update(deviceInfoUpdate);
                            DeviceInformation updatedDevice = bleDeviceDisplay.DeviceInformation;
                            //IsConnectable will be established once updated accordingly here. So function needs to be added that handles all devices.
                            ConnectToSALTERDevice(updatedDevice, bleDeviceDisplay);
                            return;
                        }

                        DeviceInformation deviceInfo = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            deviceInfo.Update(deviceInfoUpdate);
                            // If device has been updated with a friendly name it's no longer unknown.
                            if (deviceInfo.Name != String.Empty)
                            {
                                KnownDevices.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                                UnknownDevices.Remove(deviceInfo);
                            }
                        }
                    }
                }
            });

        }


        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Task.Run(async () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    StartBleDeviceWatcher();
                }
            });
        }



        public class BluetoothLEDeviceDisplay : INotifyPropertyChanged
        {
            public BluetoothLEDeviceDisplay(DeviceInformation deviceInfoIn)
            {
                DeviceInformation = deviceInfoIn;

            }

            public DeviceInformation DeviceInformation { get; private set; }

            public string Id => DeviceInformation.Id;
            public string Name => DeviceInformation.Name;
            public bool IsPaired => DeviceInformation.Pairing.IsPaired;
            public bool IsConnected => (bool?)DeviceInformation.Properties["System.Devices.Aep.IsConnected"] == true;
            public bool IsConnectable => (bool?)DeviceInformation.Properties["System.Devices.Aep.Bluetooth.Le.IsConnectable"] == true;

            public IReadOnlyDictionary<string, object> Properties => DeviceInformation.Properties;



            public event PropertyChangedEventHandler PropertyChanged;

            public void Update(DeviceInformationUpdate deviceInfoUpdate)
            {
                DeviceInformation.Update(deviceInfoUpdate);

                OnPropertyChanged("Id");
                OnPropertyChanged("Name");
                OnPropertyChanged("DeviceInformation");
                OnPropertyChanged("IsPaired");
                OnPropertyChanged("IsConnected");
                OnPropertyChanged("Properties");
                OnPropertyChanged("IsConnectable");

            }


            protected void OnPropertyChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
        #endregion

        #region DeviceConnection

        private BluetoothLEDevice bluetoothLeDevice;


        readonly int E_BLUETOOTH_ATT_WRITE_NOT_PERMITTED = unchecked((int)0x80650003);
        readonly int E_BLUETOOTH_ATT_INVALID_PDU = unchecked((int)0x80650004);
        readonly int E_ACCESSDENIED = unchecked((int)0x80070005);
        readonly int E_DEVICE_NOT_AVAILABLE = unchecked((int)0x800710df); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)
        private async Task<bool> ClearBluetoothLEDeviceAsync()
        {
            if (subscribedForNotifications)
            {
                // Need to clear the CCCD from the remote device so we stop receiving notifications
                var result = await registeredCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                if (result != GattCommunicationStatus.Success)
                {
                    return false;
                }
                else
                {
                    //selectedCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                    subscribedForNotifications = false;
                }
            }
            bluetoothLeDevice?.Dispose();
            bluetoothLeDevice = null;
            return true;
        }

        private bool subscribedForNotifications = false;

        private async void ConnectToSALTERDevice(DeviceInformation device, BluetoothLEDeviceDisplay devDisplay)
        {


            if (!await ClearBluetoothLEDeviceAsync())
            {
                //Error for unable to reset state;
                return;
            }
            if (device.Name.Contains("SALTER") && devDisplay.IsConnectable == true)
            {
                StopBleDeviceWatcher(); //Device found and connectable so stop watching
                try
                {
                    // BT_Code: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
                    bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(devDisplay.Id);

                    if (bluetoothLeDevice == null)
                    {

                    }
                }
                catch (Exception ex) when (ex.HResult == E_DEVICE_NOT_AVAILABLE)
                {
                    //Notify that device is not available
                }

                if (bluetoothLeDevice != null)
                {
                    // Note: BluetoothLEDevice.GattServices property will return an empty list for unpaired devices. For all uses we recommend using the GetGattServicesAsync method.
                    // BT_Code: GetGattServicesAsync returns a list of all the supported services of the device (even if it's not paired to the system).
                    // If the services supported by the device are expected to change during BT usage, subscribe to the GattServicesChanged event.
                    GattDeviceServicesResult result = await bluetoothLeDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    /////SPECIFIC TO SALTER DEVICE. NEEDS TO BE CUSTOMISED FOR EACH DEVICE SERVICE/CHARACTERISTIC
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        Guid serviceGUID;
                        var services = result.Services;
                        foreach (var service in services)
                        {
                            string servicename = DisplayHelpers.GetServiceName(service);
                           
                            if (servicename == "SimpleKeyService")
                            {
                                var SALTERservice = service;
                                serviceGUID = service.Uuid;
                                ConnectToService(SALTERservice);
                            }
                        }
                    }
                    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    else
                    {
                        StartBleDeviceWatcher();
                    }
                }
            }


        }

        #endregion

        #region Data Retrieval

        private GattCharacteristic selectedCharacteristic;

        // Only one registered characteristic at a time.
        private GattCharacteristic registeredCharacteristic;


        private async void ConnectToService(GattDeviceService wantedservice)
        {
            var service = wantedservice;

            RemoveValueChangedHandler();

            IReadOnlyList<GattCharacteristic> characteristics = null;
            try
            {
                // Ensure we have access to the device.
                var accessStatus = await service.RequestAccessAsync();
                if (accessStatus == DeviceAccessStatus.Allowed)
                {
                    // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
                    // and the new Async functions to get the characteristics of unpaired devices as well. 
                    var result = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        characteristics = result.Characteristics;
                    }
                    else
                    {
                        //error accessing individual service

                        // On error, act as if there are no characteristics.
                        characteristics = new List<GattCharacteristic>();
                    }
                }
                else
                {
                    // Not granted access
                    //access isn't granted

                    // On error, act as if there are no characteristics.
                    characteristics = new List<GattCharacteristic>();

                }
            }
            catch (Exception ex)
            {
                //another restricted service error
                // On error, act as if there are no characteristics.
                characteristics = new List<GattCharacteristic>();
            }
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // SPECIFIC TO SALTER DEVICE. THESE NEED TO BE CUSTOMISED FOR EACH DEVICE
            foreach (GattCharacteristic c in characteristics)
            {
                Guid characteristicGUID;
                string characteristicname = DisplayHelpers.GetCharacteristicName(c);
              
                if (characteristicname == "SimpleKeyState")//The characteristic used to transfer data from measurements from scales
                {
                    var SALTERcharacteristic = c;
                    characteristicGUID = c.Uuid;
                    selectedCharacteristic = SALTERcharacteristic;
                    SubscribeToCharacteristic();
                }
            }
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        }

        //Subscribe to the "SimpleKeystate" characteristic which is where measurements are sent via the shitty salter encryption. Note there's some leftover code
        //in here from the UWP app it was modified from to accomodate salter.
        private async void SubscribeToCharacteristic()
        {

            if (!subscribedForNotifications)
            {
                // initialize status
                GattCommunicationStatus status = GattCommunicationStatus.Unreachable;
                var cccdValue = GattClientCharacteristicConfigurationDescriptorValue.None;
                if (selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                {
                    cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Indicate;
                }

                else if (selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                {
                    cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;

                }

                try
                {
                    // BT_Code: Must write the CCCD in order for server to send indications.
                    // We receive them in the ValueChanged event handler.
                    status = await selectedCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);

                    if (status == GattCommunicationStatus.Success)
                    {
                        AddValueChangedHandler();

                        //success in subscribing for value change. show alert here to user
                        //((Window.Current.Content as Frame).Content as MainPage).SetConnectionStatus("Connected");
                        updateConnectionStatus("CONNECTED");

                    }
                    else
                    {
                        //error registering for value changes
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support indicate, but it actually doesn't.
                    //unexpected error
                }
            }
            else
            {
                try
                {
                    // BT_Code: Must write the CCCD in order for server to send notifications.
                    // We receive them in the ValueChanged event handler.
                    // Note that this sample configures either Indicate or Notify, but not both.
                    var result = await
                            selectedCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                GattClientCharacteristicConfigurationDescriptorValue.None);
                    if (result == GattCommunicationStatus.Success)
                    {
                        subscribedForNotifications = false;
                        RemoveValueChangedHandler();
                        //un-registered for BT notifications (value changes)
                    }
                    else
                    {
                        //error un-registering for BT notifications
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // This usually happens when a device reports that it support notify, but it actually doesn't.
                    //error handling here
                }
            }
        }

   
        //set up the csv for storing measurements. individual csv file per measurement, not a master file.
        string[,] arrayMeasurements = new string[4, 7];
        private void initialiseSurveyorInfo()
        {
            arrayMeasurements[0, 0] = "MeasureType";
            arrayMeasurements[0, 1] = "Measurement";
            arrayMeasurements[0, 2] = "Qtr";
            arrayMeasurements[0, 3] = "MB";
            arrayMeasurements[0, 4] = "HHID";
            arrayMeasurements[0, 5] = "RespondentID";
            arrayMeasurements[0, 6] = "MeasurementInputType";
            string[] respondentInfo = GetRespondentIdentifiers();
            arrayMeasurements[1, 2] = respondentInfo[0];
            arrayMeasurements[1, 3] = respondentInfo[1];
            arrayMeasurements[1, 4] = respondentInfo[2];
            arrayMeasurements[1, 5] = respondentInfo[3];
            arrayMeasurements[2, 2] = respondentInfo[0];
            arrayMeasurements[2, 3] = respondentInfo[1];
            arrayMeasurements[2, 4] = respondentInfo[2];
            arrayMeasurements[2, 5] = respondentInfo[3];
            //arrayMeasurements[3, 2] = respondentInfo[0];
            //arrayMeasurements[3, 3] = respondentInfo[1];
            //arrayMeasurements[3, 4] = respondentInfo[2];
            //arrayMeasurements[3, 5] = respondentInfo[3];


        }

        //Retrieves all respondent specific information generated from sample manager
        private string[] GetRespondentIdentifiers()
        {
            string respIDs = File.ReadLines(@"C:\NZHS\surveyinstructions\MeasurementInfo.txt").First();
            string[] respIDSplit = respIDs.Split('+');
            return respIDSplit;
        }

        //This is the actual event fired by a BT transmission from scales. It handles the stream and puts it into correct format with help from dispatcher Timer event
        //and 5 consecutive measurement check get final result function.
        bool isThirdMeasurement = false; //This bool needs to be set when taking third measurement, and re-set for any manual entry for 1st two measurements.
        static List<decimal> measurementList = new List<decimal>();
        static List<decimal> finalMeasurementList = new List<decimal>();
        string absolutefinal;
        List<string[]> allMeasurements = new List<string[]>();

        private async void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            //transfer characteristic value of Ibuffer type to a byte array
            byte[] array = args.CharacteristicValue.ToArray();
            //Legitimate measurements will have a byte array length of 6. Byte array length of four is on/off event
            if (array.Length == 6)
            {
                
                string hexarray = ByteArrayToString(array).Substring(8);
                int arraylength = hexarray.Length;
                decimal d = (decimal)Int64.Parse(hexarray, System.Globalization.NumberStyles.HexNumber) / 20; //The "encryption" for the salter scales
                measurementList.Add(d); //This list continues to populate until cleared by timer event fire.
                decimal tempFinal = GetFinalresult();//start timer in Getfinalresult once condition is met. Only log result if timer has elapsed two seconds
            }
            finalMeasurementList = measurementList; //pre-cautionary re-assignment of measurement list to be used in acquiring the absolute final
        }

        //This awaits the 5 consecutive equal measruements from the characteristic value changed handler, i.e. the BT transmission. majority of the time
        //5 consecutive measurements are enough to determine a final value, sometimes though the scale quickly sends a final value that is different and displays
        //different to the 5 consecutives. This is handled by implementing the dispatcherTimer and recieving absolute final value.

        System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();

        bool transmissionComplete = false;
        private decimal GetFinalresult()
        {
            //set up dispatcherTimer so it can run on seperate thread.
            int measureCount = measurementList.Count;
            if (measureCount > 5)
            {
                try
                {
                    //Alerts that 5 results are the same which is the general observed transmission of final result. a slight movement howerver can register a different
                    //final value for transmission therefore start a timer and allow Characteristic_Valuechanged to continue populating measurement list.
                    decimal measurecalc = (measurementList[measureCount - 1] + measurementList[measureCount - 2]
                        + measurementList[measureCount - 3] + measurementList[measureCount - 4] + measurementList[measureCount - 5]) / measurementList[measureCount - 1];
                    if (measurecalc == 5)
                    {
                        //Set up timespan of 2 seconds to await any other final results that may be transmitted                       
                        dispatcherTimer.Interval = new TimeSpan(0, 0, 3);//3 seconds seems to be reliable, yet every now and then we get a 0.1 error.
                        dispatcherTimer.IsEnabled = true;
                        dispatcherTimer.Start();
                        return measurementList[measureCount - 1];                      
                    }
                }
                catch (Exception e)//If user jumps off scales and it goes to 0.0kg then algorithm will try to divide by zero. Return 0 for a non-measurement
                {
                    return 0;
                }
            }
            return 0;
        }

        //This timer is set once the 5 consecutive measurements have been detected. The scales sometimes display a value they don't actually transmit if the respondent
        //Steps on them in an awakward way or has a last second movement. This timer fires to ensure the last permitted result is required. I.e a measurement
        //defined by a byte length of six. Any other byte lengths are on/off events.
        bool clearWasclicked = false;
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            
            dispatcherTimer.Stop();
            dispatcherTimer.IsEnabled = false;
            int measureCount = finalMeasurementList.Count; //enitre measurement count once timer has elapsed
            decimal tempFinal = finalMeasurementList[measureCount - 1];//Measurement list only permits 6 byte values, so this will be the last transmitted value.
            if (tempFinal != 0) 
            {
                absolutefinal = tempFinal.ToString("0.0");
                finalMeasurementList.Clear();//Clearing the list stops GetFinalresult() from retrieving multiple final values
                string[] loggedMeasurement = { "WT", absolutefinal };
                allMeasurements.Add(loggedMeasurement);
                if ((allMeasurements.Count == 1) && (isThirdMeasurement == false))
                {
                    SetW1Measurement(loggedMeasurement[1]);//first measurement will only be set from loggedMeasurement when one measure has been taken
                    arrayMeasurements[1, 0] = "WT";
                    arrayMeasurements[1, 1] = loggedMeasurement[1];
                    if (clearWasclicked == false)
                    {
                        SetW2Measurement("Wait..");
                        clear1.IsEnabled = false;
                        clear2.IsEnabled = false;
                        MessageBox.Show("Please take 5 seconds for respondent to re-position themselves for re-taking measurement.\n\n" +
                        "2nd measurement will be enabled 5 seconds after closing this message.");
                        Thread.Sleep(5000);
                        clear1.IsEnabled = true;
                        clear2.IsEnabled = true;
                        SetW2Measurement("-Ready-");
                    }
                    else
                    {
                        clearWasclicked = false;
                    }
                }
                if ((allMeasurements.Count == 2) && (isThirdMeasurement == false))
                {
                    SetW2Measurement(loggedMeasurement[1]);//2nd measurement only set when 2 measurements have been taken
                    arrayMeasurements[2, 0] = "WT";
                    arrayMeasurements[2, 1] = loggedMeasurement[1];
                    //string[,] arrayMeasurements = CreateRectangularArray(allMeasurements);
                    //string csvMeasurements = ArrayToCsv(arrayMeasurements);
                    //WriteCSVFile(csvMeasurements);
                    allMeasurements.Clear();
                }
                if (isThirdMeasurement == true)//The third measurement option has been open due to greater than 1% difference.
                {
                    SetW3Measurement(loggedMeasurement[1]);//3rd measurement setting
                    arrayMeasurements[3, 0] = "WT";
                    arrayMeasurements[3, 1] = loggedMeasurement[1];
                    allMeasurements.Clear(); //resets the list, 
                }
            }
        }

        //Three functions below handle all the manual input cases. allowing only numeric values and decimal place. 
        string previousInput = "";
        private void W1Measurement_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexOverride == false) //Regex override allows the clear button to be used as it doesn't work if regex is applied.
            {
                Regex r = new Regex("^-{0,1}\\d+\\.{0,1}\\d*$"); // This is the main part, can be altered to match any desired form or limitations
                Match m = r.Match(W1Measurement_TextBox.Text);
                if (m.Success)
                {
                    previousInput = W1Measurement_TextBox.Text;
                }
                else
                {
                    W1Measurement_TextBox.Text = previousInput;
                }
            }
            if (manualMeasurement == false)
            {
                //Any appropriate handling on textboxes if manualMeasurement false. The textBoxes will be disabled regardless so probably unneccessary.
            }
        }

        string previousInput1 = "";
        private void W2Measurement_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexOverride == false)//Regex override allows the clear button to be used as it doesn't work if regex is applied.
            {
                Regex r = new Regex("^-{0,1}\\d+\\.{0,1}\\d*$"); // This is the main part, can be altered to match any desired form or limitations
                Match m = r.Match(W2Measurement_TextBox.Text);
                if (m.Success)
                {
                    previousInput1 = W2Measurement_TextBox.Text;
                }
                else
                {
                    W2Measurement_TextBox.Text = previousInput1;
                }
            }
            if (manualMeasurement == false)
            {
                //Any appropriate handling on textboxes if manualMeasurement false. The textBoxes will be disabled regardless so probably unneccessary.
            }
        }

        string previousInput2 = "";
        private void W3Measurement_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (regexOverride == false)//Regex override allows the clear button to be used as it doesn't work if regex is applied.
            {
                Regex r = new Regex("^-{0,1}\\d+\\.{0,1}\\d*$"); // This is the main part, can be altered to match any desired form or limitations
                Match m = r.Match(W3Measurement_TextBox.Text);
                if (m.Success)
                {
                    previousInput2 = W3Measurement_TextBox.Text;
                }
                else
                {
                    W3Measurement_TextBox.Text = previousInput2;
                }
            }
            if (manualMeasurement == false)
            {
                //Any appropriate handling on textboxes if manualMeasurement false. The textBoxes will be disabled regardless so probably unneccessary.
            }
        }

        //Salter scales sepcific csv file write
        private async void WriteCSVFile(string csvMeasurements)
        {
            System.IO.Directory.CreateDirectory(@"C:\BodyMeasurements\WeightMeasurements");
            string CSVFileName = @"C:\BodyMeasurements\WeightMeasurements\" + "WeightMeasurements_" + DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss") + ".csv";

            System.IO.File.WriteAllText(CSVFileName, csvMeasurements);
        }

        //Wrtie 2d array to format expected of csv
        static string ArrayToCsv(string[,] values)
        {
            // Get the bounds.
            int num_rows = values.GetUpperBound(0) + 1;
            int num_cols = values.GetUpperBound(1) + 1;

            // Convert the array into a CSV string.
            StringBuilder sb = new StringBuilder();
            for (int row = 0; row < num_rows; row++)
            {
                // Add the first field in this row.
                sb.Append(values[row, 0]);

                // Add the other fields in this row separated by commas.
                for (int col = 1; col < num_cols; col++)
                    sb.Append("," + values[row, col]);

                // Move to the next line.
                sb.AppendLine();
            }

            // Return the CSV format string.
            return sb.ToString();
        }

        //Generate a 2d array that can be sent through arraytocsv to store values
        static T[,] CreateRectangularArray<T>(IList<T[]> arrays)
        {
            // TODO: Validation and special-casing for arrays.Count == 0
            int minorLength = arrays[0].Length;
            T[,] ret = new T[arrays.Count, minorLength];
            for (int i = 0; i < arrays.Count; i++)
            {
                var array = arrays[i];
                if (array.Length != minorLength)
                {
                    throw new ArgumentException
                        ("All arrays must be the same length");
                }
                for (int j = 0; j < minorLength; j++)
                {
                    ret[i, j] = array[j];
                }
            }
            return ret;
        }



        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        //Characteristic value changed handler instantiation for handling event of new BT data stream
        private void AddValueChangedHandler()
        {
            //ValueChangedSubscribeToggle.Content = "Unsubscribe from value changes";
            if (!subscribedForNotifications)
            {
                registeredCharacteristic = selectedCharacteristic;
                registeredCharacteristic.ValueChanged += Characteristic_ValueChanged;
                subscribedForNotifications = true;
            }
        }

        //Get rid of characteristic value changed handler, i.e. the actual transmission of BT data event
        private void RemoveValueChangedHandler()
        {
            //ValueChangedSubscribeToggle.Content = "Subscribe to value changes";
            if (subscribedForNotifications)
            {
                registeredCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                registeredCharacteristic = null;
                subscribedForNotifications = false;
            }
        }

        private decimal ConvertStrToDec(string value)
        {
            decimal convert = Convert.ToDecimal(value);
            return convert;
        }

        //check that there is less than 1% difference between measurements taken
        private bool CheckGreaterOnePercentDiff(decimal value1, decimal value2)
        {
            if (value1 > value2)
            {
                decimal percent = ((value1 / value2) * 100);
                if (percent > 101)
                {
                    return true; //true indicating that there is a higher than 1% difference
                }
                else
                {
                    return false; //false indicating that the difference is within 1%
                }
            }
            else if (value2 > value1)
            {
                decimal percent = ((value2 / value1) * 100);
                if (percent > 101)
                {
                    return true; //true indicating that there is a higher than 1% difference
                }
                else
                {
                    return false; //false indicating that the difference is within 1%
                }
            }
            else
            {
                return false; // All other cases false as value1 and value2 will be equal
            }
        }

        private bool check1DecimalPlace(string value)
        {
            try
            {
                int DPindex = value.IndexOf('.');
                int i;
                if (int.TryParse(value[DPindex + 1].ToString(), out i))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        //These regions are left here in case there is any further development. Do not worry about them.

        #region Display Helpers

        public static class DisplayHelpers
        {
            public static string GetServiceName(GattDeviceService service)
            {
                if (IsSigDefinedUuid(service.Uuid))
                {
                    GattNativeServiceUuid serviceName;
                    if (Enum.TryParse(Utilities.ConvertUuidToShortId(service.Uuid).ToString(), out serviceName))
                    {
                        return serviceName.ToString();
                    }
                }
                return "Custom Service: " + service.Uuid;
            }

            private static bool IsSigDefinedUuid(Guid uuid)
            {
                var bluetoothBaseUuid = new Guid("00000000-0000-1000-8000-00805F9B34FB");

                var bytes = uuid.ToByteArray();
                // Zero out the first and second bytes
                // Note how each byte gets flipped in a section - 1234 becomes 34 12
                // Example Guid: 35918bc9-1234-40ea-9779-889d79b753f0
                //                   ^^^^
                // bytes output = C9 8B 91 35 34 12 EA 40 97 79 88 9D 79 B7 53 F0
                //                ^^ ^^
                bytes[0] = 0;
                bytes[1] = 0;
                var baseUuid = new Guid(bytes);
                return baseUuid == bluetoothBaseUuid;
            }

            public static string GetCharacteristicName(GattCharacteristic characteristic)
            {
                if (IsSigDefinedUuid(characteristic.Uuid))
                {
                    GattNativeCharacteristicUuid characteristicName;
                    if (Enum.TryParse(Utilities.ConvertUuidToShortId(characteristic.Uuid).ToString(),
                        out characteristicName))
                    {
                        return characteristicName.ToString();
                    }
                }

                if (!string.IsNullOrEmpty(characteristic.UserDescription))
                {
                    return characteristic.UserDescription;
                }

                else
                {
                    return "Custom Characteristic: " + characteristic.Uuid;
                }
            }

            public enum GattNativeServiceUuid : ushort
            {
                None = 0,
                AlertNotification = 0x1811,
                Battery = 0x180F,
                BloodPressure = 0x1810,
                CurrentTimeService = 0x1805,
                CyclingSpeedandCadence = 0x1816,
                DeviceInformation = 0x180A,
                GenericAccess = 0x1800,
                GenericAttribute = 0x1801,
                Glucose = 0x1808,
                HealthThermometer = 0x1809,
                HeartRate = 0x180D,
                HumanInterfaceDevice = 0x1812,
                ImmediateAlert = 0x1802,
                LinkLoss = 0x1803,
                NextDSTChange = 0x1807,
                PhoneAlertStatus = 0x180E,
                ReferenceTimeUpdateService = 0x1806,
                RunningSpeedandCadence = 0x1814,
                ScanParameters = 0x1813,
                TxPower = 0x1804,
                SimpleKeyService = 0xFFE0

            }

            /// <summary>
            ///     This enum is nice for finding a string representation of a BT SIG assigned value for Characteristic UUIDs
            ///     Reference: https://developer.bluetooth.org/gatt/characteristics/Pages/CharacteristicsHome.aspx
            /// </summary>
            public enum GattNativeCharacteristicUuid : ushort
            {
                None = 0,
                AlertCategoryID = 0x2A43,
                AlertCategoryIDBitMask = 0x2A42,
                AlertLevel = 0x2A06,
                AlertNotificationControlPoint = 0x2A44,
                AlertStatus = 0x2A3F,
                Appearance = 0x2A01,
                BatteryLevel = 0x2A19,
                BloodPressureFeature = 0x2A49,
                BloodPressureMeasurement = 0x2A35,
                BodySensorLocation = 0x2A38,
                BootKeyboardInputReport = 0x2A22,
                BootKeyboardOutputReport = 0x2A32,
                BootMouseInputReport = 0x2A33,
                CSCFeature = 0x2A5C,
                CSCMeasurement = 0x2A5B,
                CurrentTime = 0x2A2B,
                DateTime = 0x2A08,
                DayDateTime = 0x2A0A,
                DayofWeek = 0x2A09,
                DeviceName = 0x2A00,
                DSTOffset = 0x2A0D,
                ExactTime256 = 0x2A0C,
                FirmwareRevisionString = 0x2A26,
                GlucoseFeature = 0x2A51,
                GlucoseMeasurement = 0x2A18,
                GlucoseMeasurementContext = 0x2A34,
                HardwareRevisionString = 0x2A27,
                HeartRateControlPoint = 0x2A39,
                HeartRateMeasurement = 0x2A37,
                HIDControlPoint = 0x2A4C,
                HIDInformation = 0x2A4A,
                IEEE11073_20601RegulatoryCertificationDataList = 0x2A2A,
                IntermediateCuffPressure = 0x2A36,
                IntermediateTemperature = 0x2A1E,
                LocalTimeInformation = 0x2A0F,
                ManufacturerNameString = 0x2A29,
                MeasurementInterval = 0x2A21,
                ModelNumberString = 0x2A24,
                NewAlert = 0x2A46,
                PeripheralPreferredConnectionParameters = 0x2A04,
                PeripheralPrivacyFlag = 0x2A02,
                PnPID = 0x2A50,
                ProtocolMode = 0x2A4E,
                ReconnectionAddress = 0x2A03,
                RecordAccessControlPoint = 0x2A52,
                ReferenceTimeInformation = 0x2A14,
                Report = 0x2A4D,
                ReportMap = 0x2A4B,
                RingerControlPoint = 0x2A40,
                RingerSetting = 0x2A41,
                RSCFeature = 0x2A54,
                RSCMeasurement = 0x2A53,
                SCControlPoint = 0x2A55,
                ScanIntervalWindow = 0x2A4F,
                ScanRefresh = 0x2A31,
                SensorLocation = 0x2A5D,
                SerialNumberString = 0x2A25,
                ServiceChanged = 0x2A05,
                SoftwareRevisionString = 0x2A28,
                SupportedNewAlertCategory = 0x2A47,
                SupportedUnreadAlertCategory = 0x2A48,
                SystemID = 0x2A23,
                TemperatureMeasurement = 0x2A1C,
                TemperatureType = 0x2A1D,
                TimeAccuracy = 0x2A12,
                TimeSource = 0x2A13,
                TimeUpdateControlPoint = 0x2A16,
                TimeUpdateState = 0x2A17,
                TimewithDST = 0x2A11,
                TimeZone = 0x2A0E,
                TxPowerLevel = 0x2A07,
                UnreadAlertStatus = 0x2A45,
                AggregateInput = 0x2A5A,
                AnalogInput = 0x2A58,
                AnalogOutput = 0x2A59,
                CyclingPowerControlPoint = 0x2A66,
                CyclingPowerFeature = 0x2A65,
                CyclingPowerMeasurement = 0x2A63,
                CyclingPowerVector = 0x2A64,
                DigitalInput = 0x2A56,
                DigitalOutput = 0x2A57,
                ExactTime100 = 0x2A0B,
                LNControlPoint = 0x2A6B,
                LNFeature = 0x2A6A,
                LocationandSpeed = 0x2A67,
                Navigation = 0x2A68,
                NetworkAvailability = 0x2A3E,
                PositionQuality = 0x2A69,
                ScientificTemperatureinCelsius = 0x2A3C,
                SecondaryTimeZone = 0x2A10,
                String = 0x2A3D,
                TemperatureinCelsius = 0x2A1F,
                TemperatureinFahrenheit = 0x2A20,
                TimeBroadcast = 0x2A15,
                BatteryLevelState = 0x2A1B,
                BatteryPowerState = 0x2A1A,
                PulseOximetryContinuousMeasurement = 0x2A5F,
                PulseOximetryControlPoint = 0x2A62,
                PulseOximetryFeatures = 0x2A61,
                PulseOximetryPulsatileEvent = 0x2A60,
                SimpleKeyState = 0xFFE1,
                Weight = 0x2A98,
                WeightMeasurement = 0x2A9D
            }



        }

        public static class Utilities
        {
            /// <summary>
            ///     Converts from standard 128bit UUID to the assigned 32bit UUIDs. Makes it easy to compare services
            ///     that devices expose to the standard list.
            /// </summary>
            /// <param name="uuid">UUID to convert to 32 bit</param>
            /// <returns></returns>
            public static ushort ConvertUuidToShortId(Guid uuid)
            {
                // Get the short Uuid
                var bytes = uuid.ToByteArray();
                var shortUuid = (ushort)(bytes[0] | (bytes[1] << 8));
                return shortUuid;
            }

            /// <summary>
            ///     Converts from a buffer to a properly sized byte array
            /// </summary>
            /// <param name="buffer"></param>
            /// <returns></returns>
            public static byte[] ReadBufferToBytes(IBuffer buffer)
            {
                var dataLength = buffer.Length;
                var data = new byte[dataLength];
                using (var reader = DataReader.FromBuffer(buffer))
                {
                    reader.ReadBytes(data);
                }
                return data;
            }
        }

        #endregion

        #region formatting

        private string formatSalterValue(IBuffer buffer)
        {
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);
            string hex = ByteArrayToString(data);
            return hex;

        }

        private string FormatValueByPresentation(IBuffer buffer, GattPresentationFormat format)
        {
            // BT_Code: For the purpose of this sample, this function converts only UInt32 and
            // UTF-8 buffers to readable text. It can be extended to support other formats if your app needs them.
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);
            if (format != null)
            {
                if (format.FormatType == GattPresentationFormatTypes.UInt32 && data.Length >= 4)
                {

                }
                else if (format.FormatType == GattPresentationFormatTypes.Utf8)
                {
                    try
                    {
                        return Encoding.UTF8.GetString(data);
                    }
                    catch (ArgumentException)
                    {
                        return "(error: Invalid UTF-8 string)";
                    }
                }
                else if (format.FormatType == GattPresentationFormatTypes.UInt16)
                {
                    try
                    {
                        return BitConverter.ToInt16(data, 0).ToString();
                    }
                    catch (ArgumentException)
                    {
                        return "(error: Invalid uint16 string)";
                    }
                }
                else
                {
                    // Add support for other format types as needed.
                    return "Unsupported format: " + CryptographicBuffer.EncodeToHexString(buffer);
                }
            }
            else if (data != null)
            {
                // We don't know what format to use. Let's try some well-known profiles, or default back to UTF-8.
                if (selectedCharacteristic.Uuid.Equals(GattCharacteristicUuids.HeartRateMeasurement))
                {
                    try
                    {
                        return "Heart Rate: " + ParseHeartRateValue(data).ToString();
                    }
                    catch (ArgumentException)
                    {
                        return "Heart Rate: (unable to parse)";
                    }
                }
                else if (selectedCharacteristic.Uuid.Equals(GattCharacteristicUuids.BatteryLevel))
                {
                    try
                    {
                        // battery level is encoded as a percentage value in the first byte according to
                        // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.battery_level.xml
                        return "Battery Level: " + data[0].ToString() + "%";
                    }
                    catch (ArgumentException)
                    {
                        return "Battery Level: (unable to parse)";
                    }
                }
                else if (selectedCharacteristic.Uuid.Equals(DisplayHelpers.GattNativeCharacteristicUuid.Weight))
                {
                    try
                    {
                        // battery level is encoded as a percentage value in the first byte according to
                        // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.battery_level.xml
                        return "Weight: " + data[0].ToString() + "KG";
                    }
                    catch (ArgumentException)
                    {
                        return "weight: (unable to parse)";
                    }
                }
                // This is our custom calc service Result UUID. Format it like an Int
                else if (selectedCharacteristic.Uuid.Equals(Constants.ResultCharacteristicUuid))
                {
                    return BitConverter.ToInt32(data, 0).ToString();
                }
                // No guarantees on if a characteristic is registered for notifications.
                else if (registeredCharacteristic != null)
                {
                    // This is our custom calc service Result UUID. Format it like an Int
                    if (registeredCharacteristic.Uuid.Equals(Constants.ResultCharacteristicUuid))
                    {
                        return BitConverter.ToInt32(data, 0).ToString();
                    }
                }
                else
                {
                    try
                    {
                        return "Unknown format: " + Encoding.UTF8.GetString(data);
                    }
                    catch (ArgumentException)
                    {
                        return "Unknown format";
                    }
                }
            }
            else
            {
                return "Empty data received";
            }


            try
            {

                for (int i = 0; i < data.Length; i++)
                {
                    string resulttest = BitConverter.ToInt16(data, i).ToString();
                    string resulttest1 = BitConverter.ToInt32(data, i).ToString();
                    //string resulttest2 = BitConverter.ToInt64(data, i).ToString();
                    //string resulttest3 = System.Text.Encoding.ASCII.GetString(data, i, (data.Length - i -1));

                   
                }


            }
            catch (ArgumentException)
            {
                return "(error: Invalid uint16 string)";
            }

            return "Unknown format";
        }

        /// <summary>
        /// Process the raw data received from the device into application usable data,
        /// according the the Bluetooth Heart Rate Profile.
        /// https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.heart_rate_measurement.xml&u=org.bluetooth.characteristic.heart_rate_measurement.xml
        /// This function throws an exception if the data cannot be parsed.
        /// </summary>
        /// <param name="data">Raw data received from the heart rate monitor.</param>
        /// <returns>The heart rate measurement value.</returns>
        private static ushort ParseHeartRateValue(byte[] data)
        {
            // Heart Rate profile defined flag values
            const byte heartRateValueFormat = 0x01;

            byte flags = data[0];
            bool isHeartRateValueSizeLong = ((flags & heartRateValueFormat) != 0);

            if (isHeartRateValueSizeLong)
            {
                return BitConverter.ToUInt16(data, 1);
            }
            else
            {
                return data[1];
            }
        }

        #endregion

        #region Constants

        public class Constants
        {
            // BT_Code: Initializes custom local parameters w/ properties, protection levels as well as common descriptors like User Description. 
            public static readonly GattLocalCharacteristicParameters gattOperandParameters = new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Write |
                                           GattCharacteristicProperties.WriteWithoutResponse,
                WriteProtectionLevel = GattProtectionLevel.Plain,
                UserDescription = "Operand Characteristic"
            };

            public static readonly GattLocalCharacteristicParameters gattOperatorParameters = new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Write |
                                           GattCharacteristicProperties.WriteWithoutResponse,
                WriteProtectionLevel = GattProtectionLevel.Plain,
                UserDescription = "Operator Characteristic"
            };

            public static readonly GattLocalCharacteristicParameters gattResultParameters = new GattLocalCharacteristicParameters
            {
                CharacteristicProperties = GattCharacteristicProperties.Read |
                                           GattCharacteristicProperties.Notify,
                WriteProtectionLevel = GattProtectionLevel.Plain,
                UserDescription = "Result Characteristic"
            };

            public static readonly Guid CalcServiceUuid = Guid.Parse("caecface-e1d9-11e6-bf01-fe55135034f0");

            public static readonly Guid Op1CharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f1");
            public static readonly Guid Op2CharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f2");
            public static readonly Guid OperatorCharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f3");
            public static readonly Guid ResultCharacteristicUuid = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f4");
        };





        #endregion

  
    }
}
