using System;
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
using System.Net.Sockets;
using System.Net;
using qAGVCenter.Areas.Model;
using qAGVCenter.Cache;
using System.Data;
using System.IO;
using System.Diagnostics;
using Qs = Qcore.SerialPortMethod;
using System.Windows.Threading;
using System.Threading;
using System.Text.RegularExpressions;
namespace qAGVCenter
{
    enum AGVaction { None = 0, ConfigMaster = 1, Run = 2, Stop = 3, Route = 5, Ignore = 4, Run_back = 9, On_safety = 10, configJig = 155 };
    enum qObject { AGV, LINE, Client, NONE};
    public partial class MainWindow : Window
    {
        #region Setup Changing
        public static bool AGVChanged = false;

        #endregion
        Regex regex = new Regex("[0-9]");
        List<mTask> LIST_THREADING_SOCKET = new List<mTask>();
        Dictionary<int, Point> card_position = new Dictionary<int, Point>();
        Stopwatch sw = new Stopwatch();
        Dictionary<int, DateTime> route_already = new Dictionary<int, DateTime>();
        DateTime[] Position_Time_fisnish_tranfer = new DateTime[100];
        List<in_cross> cross_wait_run = new List<in_cross>();
        Queue<string> QUIRegistedItem = new Queue<string>();
        public List<route_model> route_obj = new List<route_model>();
        bool[] tranfer_finished = new bool[100];
        public bool position_update = false;
        DispatcherTimer timer_CheckConnection = new DispatcherTimer();
        public MainWindow()
        {
            __init__();
            InitializeComponent();
            ADD_HARDWARE_INTERFACE();
            timer_CheckConnection.Interval = new TimeSpan(0, 0, 15);
            timer_CheckConnection.Tick += timer_CheckConnection_Tick;
            timer_CheckConnection.Start();
        }
        #region Add AGV interface
        //Add lable and must be update the position by update in Update function
        private void AddLabel(int marginLeft, int level, string name, string content, int top = 0, qObject obj = qObject.NONE)
        {
            Label lb = new Label();
            int[] normal_font = new int[] { 25, 20, 20, 20 };
            lb.VerticalAlignment = VerticalAlignment.Top;
            lb.HorizontalAlignment = HorizontalAlignment.Left;
            lb.Height = 35;
            try
            {
                lb.Name = "AGV" + name;
                if (obj != qObject.NONE)
                {
                    switch (obj)
                    {
                        case qObject.LINE:
                            lb.Content = StackPanelWithImageAndContent(content, "image/Line.png", Orientation.Horizontal, new Size(20, 30));
                            lb.FontSize = normal_font[0];
                            lb.Height = 30;
                            break;
                        case qObject.AGV:
                            lb.Content = StackPanelWithImageAndContent(content, "image/CreativeSoftAGV.png", Orientation.Vertical, new Size(30, 30));
                            lb.Height = 50;
                            lb.FontSize = normal_font[1];
                            break;
                        default:
                            break;
                    }

                }
                lb.Margin = new Thickness(marginLeft, top, 0, 0);
                grdMap.Children.Add(lb);
            }

            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Fail on Add Label", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        object StackPanelWithImageAndContent(string content, string imagePath, Orientation dir, Size imgSize, string name = null)
        {
            StackPanel stk = new StackPanel();
            stk.Orientation = dir;
            if (name != null && name.Contains("AGV"))
            {
                name = name.Remove(0, 2);


                object stkExist = grdMap.FindName("panelAGV" + name);
                if (stkExist == null)
                {
                    stk.Name = "panelAGV" + name;
                }
                else
                {
                    stk.Name = "panelAGV" + name + "_Copy";
                }
                RegisterName(stk.Name, stk); QUIRegistedItem.Enqueue(stk.Name);
            }

            TextBlock txt = new TextBlock();
            txt.Text = content;
            txt.TextAlignment = TextAlignment.Center;
            txt.Width = 20;
            txt.Margin = new Thickness(3,-10,0,0);
            txt.VerticalAlignment = VerticalAlignment.Center;
            txt.HorizontalAlignment = HorizontalAlignment.Left;
            txt.Name = "txt" + name;
            Style style = Application.Current.FindResource("Tree1") as Style;
            txt.Style = style;

            Image img = new Image();
            img.Width = imgSize.Width;
            img.Height = imgSize.Height;
            img.Source = Q.QImage(imagePath);
            img.HorizontalAlignment = HorizontalAlignment.Left;

            stk.Children.Add(txt);
            stk.Children.Add(img);
            
            stk.HorizontalAlignment = HorizontalAlignment.Left;
            return stk as object;
        }
        private void UPDATE_CONNECTION_STATUS(string nameobj, int left, int top)
        {
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                object myE = grdMap.FindName(nameobj);
                if (myE != null && myE is StackPanel)
                {
                    StackPanel myAGV = myE as StackPanel;
                    myAGV.Margin = new Thickness(left, top,0,0);
                }
            }));
        }
        private void ADD_HARDWARE_INTERFACE()
        {
            double limit = stkproduction.Width-100;
            int left = 8, top = 8;
            foreach(AGV item in RAMStorage.dsAGV)
            {
                AddLabel(marginLeft: left + 35, 2, name: item.Id.ToString(), content: item.No.ToString(), top, qObject.AGV);
                if(left >limit)
                {
                    left = 8;
                    top += 50;
                }
                else
                    left += 50;
            }
           
        }

        #endregion
        private void timer_CheckConnection_Tick(object sender, EventArgs e)
        {
            timer_CheckConnection.Stop();
            foreach(MQTT item in RAMStorage.dsMQTT)
            {
                if (item.Connected == "Fail" || item.GetTimeOut >5)
                {
                    item.Connected = "Fail";
                }
            }
            if(AGVChanged)
            {
                grdMap.Children.Clear();
                while(QUIRegistedItem.Count>0)
                    UnregisterName(QUIRegistedItem.Dequeue());
                ADD_HARDWARE_INTERFACE();

                AGVChanged = false;
            }


            timer_CheckConnection.Start();
        }

        private void __init__()
        {
            //chkAGVPosition.IsChecked = position_update;
            //DataTable dt = Helper.DTSQLite("select * from tblAGV");
            DataTable dt = HelperSql.tbProcedure("exeAgv", new[,] { {"@action" , "get"} });
            foreach (DataRow row in dt.Rows)
            {
                RAMStorage.dsAGV.Add(new AGV(row));
            }
            //DataTable dt2 = Helper.DTSQLite("select * from tblbroker");
            DataTable dt2 = HelperSql.tbProcedure("exeBrocker", new[,] { { "@action", "get" } });
            foreach (DataRow row in dt2.Rows)
            {
                RAMStorage.dsMQTT.Add(new MQTT(row));
            }
            //string sql = "select * from tblcross order by card,is_cell desc";
            //DataTable dtCross = Helper.DTSQLite(sql);
            DataTable dtCross = HelperSql.tbProcedure("exeCross", new[,] { { "@action", "get" } });
            int k_ = dtCross.Rows.Count;
            card_list.cross_point = new bool[k_ + 50];
            card_list.addr_array = new int[k_ + 50];
            card_list.time_out_cross = new DateTime[k_ + 50];
            #region get Cross point first
            for (ushort i = 1; i <= k_; i++)
            {
                DataRow rows = dtCross.Rows[i - 1];
                card_list list_rb = new card_list();
                string ca = rows["card"].ToString();
                string op = rows["oposite_card"].ToString();
                string cout = rows["card_out"].ToString();
                string inzone = rows["card_in_zone"].ToString();
                string[] in_out = new string[] { rows["direct_in"].ToString(), rows["direct_out"].ToString() };
                string isc = rows["is_cell"].ToString();
                list_rb.Add_card(i, ca, op, cout, inzone, "0", in_out[0], in_out[1], isc);
                card_list.RB_All.Add(list_rb);
                card_list.time_out_cross[i] = DateTime.Now;
            }

            #endregion
            foreach (card_list item in card_list.RB_All)
            {

                for (int i = 0; i < item.Oposite_card.Length; i++)
                {
                    bool found_it = false;
                    foreach (card_list item2 in card_list.RB_All)
                    {
                        if (item2.card == item.Oposite_card[i])
                        {
                            found_it = true;
                            item.Oposite_card[i] = item2.id;
                        }

                    }
                    if (!found_it)
                    {
                        item.Oposite_card[i] = 0;
                    }
                }
            }

            foreach (DataRow row in dtCross.Rows)
            {
                RAMStorage.dsCross.Add(new Crossbak(row));
            }



            //sql = "select * from tblcard_position"; 
            // DataTable dtpos = Helper.DTSQLite(sql);
            DataTable dtpos = HelperSql.tbProcedure("exePosition", new[,] { { "@action", "get" } });
            foreach (DataRow row in dtpos.Rows)
            {

            }

            //sql = "select * from tblLine";
            //DataTable dtLines = Helper.DTSQLite(sql);
            DataTable dtLines = HelperSql.tbProcedure("exeLine", new[,] { { "@action", "get" } });
            foreach (DataRow row in dtLines.Rows)
            {
                RAMStorage.dsLine.Add(new Line(row));
            }

            //sql = "select * from tblCardFunction";
            //DataTable dtFunctionCard = Helper.DTSQLite(sql);
            DataTable dtFunctionCard = HelperSql.tbProcedure("exeCardFuncion", new[,] { { "@action", "get" } });
            foreach (DataRow row in dtFunctionCard.Rows)
            {
                RAMStorage.dsFunctionCard.Add(new FunctionCard(row));
            }
            //sql = "select * from tblcard_position order by card desc";
            //DataTable dtCardPos = Helper.DTSQLite(sql);
            DataTable dtCardPos = HelperSql.tbProcedure("exePosition", new[,] { { "@action", "get" } });
            foreach (DataRow row in dtCardPos.Rows)
            {
                RAMStorage.dsCardPos.Add(new CardPos(row));
            }

        }
        private void btnReputation_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult rl = MessageBox.Show("Bạn có chắc muốn Thoát", "Cảnh báo", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if(rl == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
            
        }
        #region MQTT Method
        void CreateSocket(MQTT broker)
        {
            ProtocolType myType = ProtocolType.Tcp;
            mTask task = new mTask();
            task.Broker = broker;
            task.qSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, myType);
            if (String.IsNullOrEmpty(task.Broker.Ip) || task.Broker.Ip == "NOIP")
            {
                //UPDATE_CONNECTION_STATUS(task.STATION.STATION_NUMBER.ToString(), QSOCKET_STATUS.UNKNOW);
                return;
            }
            task.qIPEndPoint = new IPEndPoint(IPAddress.Parse(task.Broker.Ip), task.SocPort);
            task.previousIP = task.Broker.Ip;
            //cbIPdebug.Items.Add(task.STATION.NODE_IP);
            task.qStatus = QSOCKET_STATUS.READY;
            task.qThread = new Task(() => {
                while (true)
                {


                    if (task.qThreadControl == TASKCONTROL.ABORT || task.Broker == null)
                    {
                        break;// If about this task, then Exit it no more Run
                    }

                    try
                    {
                       
                        while (!task.qSocket.Connected || task.qStatus == QSOCKET_STATUS.FAIL)
                        {
                            task.qSocket.Connect(task.qIPEndPoint);
                            task.qStatus = QSOCKET_STATUS.SUCCESS;

                        }
                        task.qSocket.ReceiveTimeout = 5000;
                        task.qStatus = QSOCKET_STATUS.SUCCESS;
                       // UPDATE_CONNECTION_STATUS(task.STATION.STATION_NUMBER.ToString(), task.qStatus);
                        while (true)
                        {
                            try
                            {
                               
                                byte[] buffers = new byte[1024];
                                int bytes = task.qSocket.Receive(buffers);
                                if (bytes > 0)
                                {
                                    byte[] data = new byte[bytes];
                                    Array.Copy(buffers, 0, data, 0, bytes);
                                    RECEIVEDSOCKET(task, data);
                                }
                                if (task.qThreadControl == TASKCONTROL.ABORT)
                                {
                                    break;// If about this task, then Exit it no more Run
                                }
                                //UPDATE_CONNECTION_STATUS(task.STATION.STATION_NUMBER.ToString(), QSOCKET_STATUS.SUCCESS);

                            }
                            catch// (Exception ex)
                            {

                                task.qStatus = QSOCKET_STATUS.FAIL;
                                //UPDATE_CONNECTION_STATUS(task.STATION.STATION_NUMBER.ToString(), task.qStatus);
                                break;
                            }
                        }
                    }
                    catch (SocketException ex)
                    {
                        task.qSocket.Close();
                        task.qSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, myType);
                        task.qIPEndPoint = new IPEndPoint(IPAddress.Parse(broker.Ip), task.SocPort);
                        if (task.qStatus != QSOCKET_STATUS.FAIL)
                        {
                            task.qStatus = QSOCKET_STATUS.FAIL;
                            //UPDATE_CONNECTION_STATUS(task.STATION.STATION_NUMBER.ToString(), task.qStatus);
                        }
                        // show Exception
                        //SOCKETMSG("Socket Error" + task.STATION.NODE_IP + ">\n" + ex.Message);
                        Task.Delay(10000);

                    }
                    catch// (Exception ex)
                    {
                        task.qSocket.Close();
                        task.qSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, myType);
                        task.qIPEndPoint = new IPEndPoint(IPAddress.Parse(broker.Ip), task.SocPort);
                        // show Exception
                        //SOCKETMSG("Error" + task.STATION.IP + ">\n" + ex.Message);
                        task.qStatus = QSOCKET_STATUS.FAIL;
                        //UPDATE_CONNECTION_STATUS(task.STATION.STATION_NUMBER.ToString(), task.qStatus);
                    }
                }
            });
            LIST_THREADING_SOCKET.Add(task);
        }
        void initSocket()// init List socket
        {
            //cbIPdebug.Items.Clear();
            //lbmessageterminal.Content = String.Format("Start init the Socket");
                foreach (var mqtt in RAMStorage.dsMQTT)
                {
                    CreateSocket(mqtt);
                
                }

        }
        #endregion
        #region UDP Method
        Queue<byte> list_db= new Queue<byte>();
        bool first_serial = false;
        UdpClient newsock;
        private void StartupServer()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            int UDPPort = 9494;
            string ip_ = "";
            try
            {
                foreach (var ip in host.AddressList)
                {
                    ip_ = ip.ToString();
                }
                this.Title +="---HOST IP: "+ ip_;
            }
            catch { }
            byte[] data = new byte[1024];
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, UDPPort);
            newsock = new UdpClient(ipep);
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            Task mission = Task.Run((Action)(() => {
                while (true)
                {
                    data = newsock.Receive(ref sender);
                    SocketRecieved(sender, data);
                }
            }));
        }
        void SocketRecieved(IPEndPoint endpoint, byte[] data)
        {
            string IP = endpoint.Address.ToString();
            MQTT client = RAMStorage.dsMQTT.Where(m => m.Ip == IP).FirstOrDefault();
            if (client != null)
            {
                client.Connected = "OK";
            }
            else
            {
                MessageBox.Show("Stranger Client Connected: " + IP);
            }
            foreach (byte item in data) { list_db.Enqueue(item); }
            while (list_db.Count > 6)
            {
                if (!first_serial)
                {// neu chua tim thay byte dau tien thi Dequeu
                    byte bytes = list_db.ElementAt(0);
                    if (bytes == 0x7E)
                    {// neu tim duoc byte dau tien
                        first_serial = true;
                    }
                    else
                    {
                        list_db.Dequeue();
                    }

                }
                else
                {
                    if (list_db.Count > 6)
                    {
                        byte[] buffer = new byte[7];
                        Array.Copy(list_db.ToArray(), 1, buffer, 0, 6);
                        string content = Qs.ByteArrayToHexString(buffer);
                        if (buffer[0] != 0)
                        {
                            this.Dispatcher.Invoke(() => {
                                Darkbox(buffer, endpoint);
                            });
                        }                         
                        Removebytes(list_db, 7);
                        first_serial = false;
                    }
                }
            }
            
        }
        void Removebytes(Queue<byte> db, int qty)
        {
            for (int i = 0; i < qty && db.Count > 0; i++)
            {
                db.Dequeue();
            }
        }
        int swcounter = 0;
        void Darkbox(byte[] all_byte, IPEndPoint endpoint)
        {
            sw.Reset();
            sw.Start();
            int rb_no = all_byte[0];
            var obj_agv = RAMStorage.dsAGV.Where(m => m.No == rb_no).FirstOrDefault();
            string xbee = obj_agv.Address;
            int rote = all_byte[1];// route cua RBC
            byte direct = all_byte[3];
            string hex = Qs.intToHex(all_byte[2]) + Qs.intToHex(all_byte[3]);

            int _card = Qs.HexToInt(hex);// Qs.HexToInt(hex);

            string card = _card.ToString();// ma the cua RBC
            

            int route_for_catch = rote;
            if (card.Length < 2)
            {
                card = "0" + card;
            }
            int head_of_card = int.Parse(card.Substring(0, 2));
            string staboba = hex + "(" + _card + ")";
            int battery = all_byte[3];// Battery mustbe adjust again


            bool flex = false;
            string step_Checking = "";
            string st = "";
            if (obj_agv != null)
            {
                rb_no = obj_agv.No;
                flex = Convert.ToBoolean(obj_agv.Flexiable);
                step_Checking = " Step:";
                #region cap nhat bin , trang thai RBC
                #region bits array
                //													 		        staboba += "\nRoute: " + rote +
                //								                                    "\nBits: |01|02|03|04|05|06|07|08|09|10|11|12|13|14|15|16|\n         ";
                //								                                    foreach (int item in bits_arry)
                //								                                    {
                //								                                        staboba += "|0" + item + "";
                //								                                    }
                //								                                    lbagvshown.Text+ = staboba.Insert(0, "Card: ");

                #region update AGV interface
                obj_agv.Battery = battery;
                obj_agv.Route = rote;
                // AGV status
                //Card _card

                #endregion

               lbagvshown.Text = "RB:" + rb_no + "-XBee: " + xbee + "-Card: " + _card;
                #endregion





                #endregion
            }
            if (rb_no == 0)
            {
                //rttime se phai sua.AppendText("Error " + PCout + "List AGV" + "Khong co AGV nay trong danh sach\n" + xbee + "\n");
                sw.Stop();
                //lbagvinfor.Text = "Not Found AGV:"+sw.ElapsedMilliseconds+"ms";
                return;
            }
            if (_card == 99)// choose route
            {
                lbagvshown.Text += step_Checking + "Choosing route....";
                #region choose route

                for (int i = 0; i < card_list.addr_array.Length; i++)
                {
                    if (card_list.addr_array[i] == rb_no && card_list.cross_point[i])
                    {
                        card_list.cross_point[i] = false;
                        card_list.addr_array[i] = 0;


                    }
                }

                if (!flex)
                {
                    Position_Time_fisnish_tranfer[route_for_catch] = DateTime.Now;
                    //RequestAGV se phai sua(xbee, 2, PCout);
                    lbchoose_rote.Text = "AGV" + rb_no + " not flex";
                }

                else if (flex)// kiem tra xem xbee nay da chon route bao lau roi
                {


                    double times = 50;

                    if (route_already.ContainsKey(rb_no))
                    {

                        TimeSpan sp = DateTime.Now.Subtract(route_already[rb_no]);
                        times = sp.TotalSeconds;
                    }
                    else
                    {

                        route_already.Add(rb_no, DateTime.Now);
                    }


                    if (times > 15.0)// thoi gian vua chon route phai lon hon 30s
                    {

                        //choose_route(rote,xbee);
                        foreach (route_model route_find in route_obj)
                        {
                            if (route_find.route == route_for_catch)
                            {
                                int _route_max = route_find.list_route[0];


                                Position_Time_fisnish_tranfer[_route_max] = DateTime.Now;
                                tranfer_finished[_route_max] = false;
                                //RequestAGV se phai sua(xbee, (int)AGVaction.Route, _route_max);

                                lbchoose_rote.Text = "AGV" + rb_no + " route: " + _route_max;
                                break;
                            }
                        }
                        route_already[rb_no] = DateTime.Now;
                    }
                    else
                    {
                        //RequestAGV se phai sua(xbee, (int)AGVaction.Run);
                        lbchoose_rote.Text = "AGV" + rb_no + " route in 15 sec ago";
                    }


                }

                #endregion
            }
            else if (_card == ushort.Parse("" + rote + "99"))
            {
                if (direct == 1)
                {
                    lbagvshown.Text += step_Checking + "_RecordTime";
                    double subtract_time = DateTime.Now.Subtract(Position_Time_fisnish_tranfer[rote]).TotalSeconds;
                    Position_Time_fisnish_tranfer[rote] = DateTime.Now;
                    tranfer_finished[rote] = true;
                    newsock.Send(new byte[] { 1,1,1,1},4);
                    //RequestAGV se phai sua(xbee, 2, PCout);
                }
            }
            else
            {
                #region cross function
                int position = _card;
                if (card_list.in_cross(rb_no, _card, direct, all_byte, xbee))// in cross
                {

                    lbagvshown.Text += step_Checking + "Go";
                    //RequestAGV se phai sua(xbee, 2, PCout);
                    if (card_position.ContainsKey(position))
                    {
                        object crosslock = grdMap.FindName("cross_"+position);
                        if(crosslock != null && crosslock is TextBox)
                        {
                            TextBlock bl = crosslock as TextBlock;
                            bl.Visibility = Visibility.Visible;
                        }

                    }

                }
                position = card_list.check_out_cross(rb_no, _card, direct);
                if (position != 0)// out cross
                {
                    if (card_position.ContainsKey(position))
                    {
                        //PictureBox lbbox = panelMap.Controls.Find("lbcross_" + position, true)[0] as PictureBox;
                        //lbbox.Visible = false;
                    }
                    position = 0;
                }
                position = card_list.out_Zone(rb_no, _card);
                if (position != 0)
                {
                    if (card_position.ContainsKey(position))
                    {
                        //PictureBox lbbox = panelMap.Controls.Find("lbcross_" + position, true)[0] as PictureBox;
                        //lbbox.Visible = false;
                    }
                }



                #endregion
            
            }
            #region Update Position
            if (menuchkPosition.IsChecked)
            {
                TextBlock lbrbc = grdMap.FindName("rb" + rb_no) as TextBlock;

                if (card_position.ContainsKey(_card))
                {

                    lbrbc.Margin = new Thickness(card_position[_card].X, card_position[_card].Y, 0, 0);
                }

            }
            #endregion
            if (menuchkinterface.IsChecked)
            {
                #region count down
                //rttime se phai sua.Text = "";
                foreach (route_model _item in route_obj)
                {
                    //rttime se phai sua.AppendText(_item.cell_name + "__" + Math.Round(DateTime.Now.Subtract(Position_Time_fisnish_tranfer[_item.route]).TotalSeconds, 1) + "-" + tranfer_finished[_item.route] + "-" + "\n");
                }
                #endregion
                #region Show cross information
                if (rtCross.Visibility == Visibility.Visible)
                {
                    st = "";

                    foreach (card_list b in card_list.RB_All)
                    {
                        if (card_list.cross_point[b.id] || b.agv_addr != 0)
                        {
                            if (b.card == 0)
                                return;
                            st += b.card + ":_" + b.id + "_ " + card_list.cross_point[b.id] + "_";
                            foreach (int a in b.Oposite_card)
                            {
                                st += "p_" + a + ",";
                            }
                            st += "(";
                            foreach (int a in b.list_Card_in_zone)
                            {
                                st += "" + a + ",";
                            }


                            if (b.agv_addr != 0)
                                st += ")----AGV" + b.agv_addr + "\n";
                            else
                                st += ")----" + card_list.addr_array[b.id] + " \n";
                        }
                        else
                        {
                            foreach (in_cross itme in cross_wait_run)
                            {
                                if (b.id == itme.current_id)
                                {
                                    if (b.card == 0)
                                        return;
                                    st += b.card + ":_" + b.id + "_ " + card_list.cross_point[b.id] + "_";
                                    foreach (int a in b.Oposite_card)
                                    {
                                        st += "p_" + a + ",";
                                    }
                                    st += "(";
                                    foreach (int a in b.list_Card_in_zone)
                                    {
                                        st += "" + a + ",";
                                    }


                                    if (b.agv_addr != 0)
                                        st += ")----AGV" + b.agv_addr + "\n";
                                    else
                                        st += ")----" + card_list.addr_array[b.id] + " \n";
                                }
                            }
                        }
                    }
                    TextRange range = new TextRange(rtCross.Document.ContentStart, rtCross.Document.ContentEnd);
                    range.Text = "";
                    rtCross.AppendText(st);
                    
                }

                #endregion
            }
            sw.Stop();
            lbspeed1.Text = "" + sw.ElapsedMilliseconds + " ms";
            swcounter++;
            if (card_list.Qwaiter.Count == 0 || swcounter != 5)
                return;

            Task.Run((Action)(() => {
                
                this.Dispatcher.Invoke(()=>
                {
                    if (swcounter > 10)
                        swcounter = 0;
                    Darkbox(card_list.Qwaiter.Dequeue(), endpoint);

                });

            }));

        }
        #endregion
        #region TCP Method
        Socket socServer;
        void CreateTCP()
        {
            IPEndPoint ip = new IPEndPoint(IPAddress.Any, 9495);
            socServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socServer.Bind(ip);
            Task.Run(()=> {
                this.Dispatcher.Invoke(()=> {
                    while (true)
                    {
                        try
                        {
                            socServer.Listen(50);
                            Socket soc = socServer.Accept();
                            byte[] buffer = new byte[1024];
                            while(true)
                            {
                                soc.Receive(buffer, SocketFlags.Broadcast);
                                if(buffer.Length>0)
                                {

                                }
                            }

                        }
                        catch(Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                            break;
                            //socServer.Close();
                            //Socket soc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        }
                    }
                });
            });
        }
        void CloseTCp()
        {
            this.socServer.Disconnect(true);
            this.socServer.Close();
            this.socServer.Dispose();
        }

        #endregion
        void RECEIVEDSOCKET(mTask task, byte[] buffer)
        {
            if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0)
            {
                return;
            }
            else if (buffer.Length < 5)
                return;

            System.Text.ASCIIEncoding myEn = new System.Text.ASCIIEncoding();
            string content = myEn.GetString(buffer).TrimEnd(new char[] { '\n', '\0', '\t', '\r' }).ToUpper();
            Darkbox(buffer, task.qIPEndPoint);
        }
        public void SEND_SOCKET(Socket sock, byte[] sendout)
        {
            sock.Send(sendout);
        }
        #region Function
        private void SetUPAGV_MenuClick(object sender, RoutedEventArgs e)
        {
            Forms.AGVmanagement frm = new Forms.AGVmanagement();
            frm.Show();
        }

        private void DatTram_Click(object sender, RoutedEventArgs e)
        {
            Forms.BrokerManagement frm = new Forms.BrokerManagement();
            frm.Show();
        }

        private void menuStationSetup_Click(object sender, RoutedEventArgs e)
        {
            Forms.Linemanagement frm = new Forms.Linemanagement();
            frm.Show();
        }

        private void menuFunctionCardSetup_Click(object sender, RoutedEventArgs e)
        {
            Forms.FunctionCardManagement frm = new Forms.FunctionCardManagement();
            frm.Show();
        }

        private void menuCrossSetup_Click(object sender, RoutedEventArgs e)
        {
            Forms.Crossmanagement frmCross = new Forms.Crossmanagement();
            frmCross.Show();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            StartupServer();
        }

        private void btnAGV_Click(object sender, RoutedEventArgs e)
        {
            Forms.AGVmanagement frm = new Forms.AGVmanagement();
            frm.Show();
        }

        private void btnTrafic_Click(object sender, RoutedEventArgs e)
        {
            Forms.Crossmanagement frmCross = new Forms.Crossmanagement();
            frmCross.Show();
        }

        private void btnBroker_Click(object sender, RoutedEventArgs e)
        {
            Forms.BrokerManagement frm = new Forms.BrokerManagement();
            frm.Show();
        }

        private void btnsetupline_Click(object sender, RoutedEventArgs e)
        {
            Forms.Linemanagement frm = new Forms.Linemanagement();
            frm.Show();
        }

        private void btnfunctionCard_Click(object sender, RoutedEventArgs e)
        {
            Forms.FunctionCardManagement frm = new Forms.FunctionCardManagement();
            frm.Show();
        }

        private void btncross_Click(object sender, RoutedEventArgs e)
        {
            Forms.Crossmanagement frmCross = new Forms.Crossmanagement();
            frmCross.Show();
        }
        #endregion

        private void menuPositionSetup_Click(object sender, RoutedEventArgs e)
        {
            Forms.PositionManagement frmPos = new Forms.PositionManagement();
            frmPos.Show();
        }

        private void btnremovecross_Click(object sender, RoutedEventArgs e)
        {
            string textDel = txtoneroute.Text;
            if(textDel=="")
            {
                return;
            }
            if (textDel == "all")
            {
                foreach (card_list item in card_list.RB_All)
                {

                    card_list.cross_point[item.id] = false;
                    card_list.addr_array[item.id] = 0;
                    item.agv_addr = 0;

                }
            }
            else
            {
                int c = ushort.Parse(txtoneroute.Text);

                foreach (card_list item in card_list.RB_All)
                {
                    if (item.card == c)
                    {
                        card_list.cross_point[item.id] = false;
                        card_list.addr_array[item.id] = 0;
                        item.agv_addr = 0;
                    }
                }
            }
        }
        private bool isNumber(string content)
        {
            return !regex.IsMatch(content);
        }
        private void txtoneroute_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = isNumber(e.Text);
        }
    }
}