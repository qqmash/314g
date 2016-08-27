using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Npgsql;
using System.Threading;
using System.Net.NetworkInformation;
using System.Net;
using System.Management;
using System.Diagnostics;
using System.Windows.Forms;

namespace PostgreSlave
{
    public partial class MainWindow : Window
    {
        public string connectToMaster = ";User Id=postgres;Password=postgres;Database=";
        public string connectToSlave = ";User Id=postgres;Password=postgres;Database=";
        public string connectToDBdefault = ";User Id=postgres;Password=postgres;Database=";        
        public int countOfTest = 10;
        bool fileBacup = false;
        public bool createTestTableFlag = false;
        
        public MainWindow()
        {
            InitializeComponent();
            Button_Click.IsEnabled = false;
            Slave_Monitor.IsEnabled = false;           
        }

        public void reconect(string IP)
        {
            Form form1 = new Form();
            form1.Text = "Primary IP is busy!!!";//Основной IP занят! Проверьте, что на мастере выключен интерфейс.";
            System.Windows.Forms.TextBox mse = new System.Windows.Forms.TextBox();
            mse.Text = "Основной IP занят! Проверьте, что на мастере выключен интерфейс. После освобождения IP этому компьютеру автоматически присоится основной адрес и слев будет перезапущен как мастер.";            
            form1.Show();            
            while (true)
            {
                try
                {
                    Ping pingSender = new Ping();
                    IPAddress address = IPAddress.Parse(IP);
                    PingReply reply = pingSender.Send(address);
                    if (reply.Status == IPStatus.Success) { }
                    else break;
                }
                catch (Exception a)
                {
                    a.ToString();
                    System.Windows.Forms.MessageBox.Show("Incorrect IP: " + IP + " . Error: " + a);                    
                }
            }
            form1.Close();
        }

        private void Button_Click_Test(object sender, RoutedEventArgs e)
        {
            checkIPsAvalaible();
            Running_task.Text = "";
            Error_List.Text = "";
            Program_State.Text = "Testing...";
            if (fileBacup == false)
                {
                    if (System.Windows.MessageBox.Show("Do you want backup your work files?", "", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) backupSlaveSetting();                              
                    fileBacup = true;
                }                                    
            connectToMaster = "Server=" + Master_IP.Text + connectToMaster + Testing_DB.Text + ";";     
            connectToSlave = "Server=" + Slave_IP.Text + connectToSlave + Testing_DB.Text + ";";
            if (createTestTableFlag == false) createTestTable();
            if (testSlave() == true) Slave_State.Text = "Slave";
            else Slave_State.Text = "DOWN!";
            if (testDB(connectToSlave, "insert into testofcluster values ('1')") == 1) Slave_State.Text = "Slave";
            else
            {
                Replication_Slave_Status.Text = "No replication!";
                Slave_State.Text = "Master";                
            }
            if (testMaster() == true) 
            {
                Master_State.Text = "Master";
                NpgsqlConnection connectionToDB = new NpgsqlConnection(connectToMaster);
                connectionToDB.Open();
                NpgsqlCommand commandToExecute = new NpgsqlCommand("select (active) from pg_replication_slots", connectionToDB);
                NpgsqlDataReader data = commandToExecute.ExecuteReader();
                while (data.Read())
                {                    
                    if (data[0].ToString() == "True") Replication_Master_Status.Text = "Working";
                    else Replication_Master_Status.Text = "Didn't work!";
                }
                connectionToDB.Close();
            }
            else
            {
                string errorMessage = "Test-table 'testofcluster' on master-host is not avalible. Do you want to try to create it?";
                if (System.Windows.MessageBox.Show(errorMessage, "Eror", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) createTestTable();
                Replication_Master_Status.Text = "DOWN!";
            }            
            if ((Slave_State.Text == "Slave") && (Master_State.Text == "Master") && (Replication_Master_Status.Text == "Working")) Slave_Monitor.IsEnabled = true;            
            Program_State.Text = "Done. (" + countOfTest.ToString() + ")";
            connectToMaster = connectToDBdefault;
            connectToSlave = connectToDBdefault;            
        }

        private void Button_Click_Monitoring(object sender, RoutedEventArgs e)
        {
            connectToMaster = "Server=" + Master_IP.Text + connectToMaster + Testing_DB.Text + ";";            
            Program_State.Text = "Monitoring is running...";
            Running_task.Text = "";
            Error_List.Text = "";
            System.Windows.MessageBox.Show("Внимание! После нажатия кнопки \"ОК\" и в случае если мастер будет недоступен, слев перейдёт в режим записи.");
            while (true)
            {
                if (testMaster() == false) break;
            }
            becameSlaveToMaster();
            Program_State.Text = "Monitor has stopped.";
            DateTime localDate = DateTime.Now;
            string time = localDate.ToString();
            System.Windows.MessageBox.Show("Slave started as master in " + time);
            connectToMaster = connectToDBdefault;            
        }

        public bool checkIP(string checkingIP)
        {
            try
            {
                Ping pingSender = new Ping();
                IPAddress address = IPAddress.Parse(checkingIP);
                PingReply reply = pingSender.Send(address);
                if (reply.Status == IPStatus.Success)
                {
                    return true;
                }
                else
                {
                    Running_task.Text += "(NO " + checkingIP + ") ";                    
                    return false;
                }
            }
            catch (Exception a)
            {
                a.ToString();
                System.Windows.MessageBox.Show("Incorrect IP: " + checkingIP);
                return false;
            }
        }

        public bool becameSlaveToMaster()
        {
            Running_task.Text = "Checking primary IP(" + Primary_IP.Text + ")... ";
            //сделать циклическую проверку
            string IP = Primary_IP.Text;
            Task.Factory.StartNew(() => reconect(IP));
            while (checkIP(Primary_IP.Text) == true) ;//System.Windows.MessageBox.Show("Primary IP is still busy! Check you network connection.");             
            System.IO.File.WriteAllText(@"C:\Program Files\PostgreSQL\9.4\data\startmaster", "Go!");
            Running_task.Text = "Became slave to master...";
            ChangeIPto(Primary_IP.Text, "255.255.255.0");
            Button_Click.IsEnabled = false;
            Slave_Monitor.IsEnabled = false;
            return true;
        }

        public static IPAddress getDefaultGateway()
        {
            var interFace = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault();
            if (interFace == null) return null;
            var addressDG = interFace.GetIPProperties().GatewayAddresses.FirstOrDefault();
            return addressDG.Address;
        }

        public void setGateway(string gateway)
        {
            ManagementClass objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection objMOC = objMC.GetInstances();
            foreach (ManagementObject objMO in objMOC)
            {
                if ((bool)objMO["IPEnabled"])
                {
                    try
                    {
                        ManagementBaseObject setGateway;
                        ManagementBaseObject newGateway = objMO.GetMethodParameters("SetGateways");
                        newGateway["DefaultIPGateway"] = new string[] { gateway };
                        newGateway["GatewayCostMetric"] = new int[] { 1 };

                        setGateway = objMO.InvokeMethod("SetGateways", newGateway, null);
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
        }
        
        public void ChangeIPto(string ipAddress, string subnetMask)
        {
            IPAddress GTW = getDefaultGateway();
            ManagementClass MC = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection MOC = MC.GetInstances();
            foreach (ManagementObject MO in MOC)
            {
                if ((bool)MO["IPEnabled"])
                {                    
                    try
                    {                        
                        ManagementBaseObject setIP;
                        ManagementBaseObject newIP = MO.GetMethodParameters("EnableStatic");
                        newIP["IPAddress"] = new string[] { ipAddress };
                        newIP["SubnetMask"] = new string[] { subnetMask };
                        setIP = MO.InvokeMethod("EnableStatic", newIP, null);                        
                        setGateway(GTW.ToString());
                    }
                    catch (Exception a)
                    {
                        System.Windows.MessageBox.Show(a.ToString());                                          
                    }
                }
            }
        }

        public bool testSlave()
        {
            if (testDB(connectToSlave, "select count(*) from testofcluster") == 0) return true;            
            return false;
        }

        public bool testMaster()
        {
            if (testDB(connectToMaster, "insert into testofcluster values ('1')") == 0) return true;
            else return false;
        }

        public bool createTestTable()
        {
            if (testDB(connectToMaster, "create table testofcluster (hint int)") == 0)
            {
                createTestTableFlag = true;
                return true;
            }
            else
            {
                System.Windows.MessageBox.Show("Can not to create test-table 'testofcluster' in database '" + Testing_DB.Text + "'.");
                return false;
            }            
        }

        public int testDB(string connectionString, string executeCommand)
        {
            try
            {
                countOfTest++;
                NpgsqlConnection connectionToDB = new NpgsqlConnection(connectionString);
                connectionToDB.Open();
                NpgsqlCommand commandToExecute = new NpgsqlCommand(executeCommand, connectionToDB);
                commandToExecute.ExecuteScalar();
                if (countOfTest > 200000)
                {
                    testDB(connectionString, "delete from testofcluster");
                    countOfTest = 0;
                }
                connectionToDB.Close();                
                return 0;
            }
            catch (Exception Exept)
            {
                if (Exept.Message == "ОШИБКА: 25006: в транзакции в режиме \"только чтение\" нельзя выполнить INSERT") return 1;
                if (Exept.Message == "ОШИБКА: 42P07: отношение \"testofcluster\" уже существует") return 0;

                Running_task.Text += "Database isn't available! ";
                Error_List.Text = Exept.Message;
                return 2;
            }             
        }

        public bool backupSlaveSetting()
        {
            string directoryPath = @"C:\Program Files\PostgreSQL\9.4\backupSlaveSetting";
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Error_List.Text = "Backup exists already (backupSlaveSetting). Delete it to proved new save.";
                    return false;
                }
                else
                {
                    DirectoryInfo dir = Directory.CreateDirectory(directoryPath);            
                    File.Copy(@"C:\Program Files\PostgreSQL\9.4\data\recovery.conf", @"C:\Program Files\PostgreSQL\9.4\backupSlaveSetting\recovery.conf");
                    File.Copy(@"C:\Program Files\PostgreSQL\9.4\data\postgresql.conf", @"C:\Program Files\PostgreSQL\9.4\backupSlaveSetting\postgresql.conf");
                    File.Copy(@"C:\Program Files\PostgreSQL\9.4\data\postgresql.auto.conf", @"C:\Program Files\PostgreSQL\9.4\backupSlaveSetting\postgresql.auto.conf");
                    return true;
                }
            }
            catch (Exception exep)
            {
                Error_List.Text = "Backup is failed: " + exep.ToString();
                return false;
            }
        }

        public void checkIPsAvalaible()
        {
            checkIP(Slave_IP.Text);
            checkIP(Master_IP.Text);
            checkIP(Primary_IP.Text);
        }

        public void checkUsersIP()
        {            
            if ((Slave_IP.Text != "") && (Master_IP.Text != "") && (Primary_IP.Text != "") && (Testing_DB.Text != "")) Button_Click.IsEnabled = true;
        }

        private void Slave_IP_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkUsersIP();
        }

        private void Master_IP_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkUsersIP();
        }

        private void Primary_IP_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkUsersIP();
        }

        private void Testing_DB_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkUsersIP();
        }
    }
}
