﻿using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using NServer;
using nbpack;
using System.Drawing;	//for using Image type.

namespace nboard
{
    public class AggregatorMain 
    {
		private static float DOWNLOAD_TIMEOUT_SEC
		{
			get
			{ 
				var timeoutStr = Configurator.Instance.GetValue("download_timeout_sec", "30");
				float timeout = 0;
				if (float.TryParse(timeoutStr, out timeout))
				{
					if(timeout==0){
						timeout = (float)nbpack.NBPackMain.parse_number(timeoutStr);
						if(timeout==0){
							return 30f;
						}else{
							return timeout;
						}
					}
					return timeout;
				}
				return 30f;
			}
		}

		private static long collect_memory_limit_to_wait	//bytes
		{
			get
			{ 
				long default_mem_limit =
				(
					(
						(long)(30)				//default max limit - 200 Megabytes
					)
					*
					(
						(long)(1024 * 1024)		//* 1024 KByte/MegaByte * 1024 Bytes/KiloByte
					)
				);
				
				var memory_limit = Configurator.Instance.GetValue(
					"collect_memory_limit_to_wait",
					default_mem_limit.ToString()					//=209715200 -> .ToString()
				);
				float mem_limit = 0;
				if (float.TryParse(memory_limit, out mem_limit))
				{
					if(mem_limit==0){
						mem_limit = (long)nbpack.NBPackMain.parse_number(memory_limit);
						if(mem_limit==0){
							return (long)default_mem_limit;
						}else{
							return (long)mem_limit;
						}
					}
					return (long)mem_limit;
				}
				return (long)default_mem_limit;
			}
		}

	  public static int maximum_timeout = (int)DOWNLOAD_TIMEOUT_SEC * 1000;		//milliseconds
	  public static bool is_already_increased_if_proxy = false;					//true, if increased.
	  public static bool is_downloading_error = false;							//true, if increased.
      public static long maximum_collect_memory_limit_to_wait = (long)collect_memory_limit_to_wait;	//bytes
      public static bool Running { get; private set; }
      public static void Run(string [] p = null)
      {
        if (Running) return;
        _Main(p);
      }
	  
      public static void _Main(string [] _params_ = null) {
        bool running = true;
        Running = true;
        var agg = new Aggregator(_params_);
        agg.ProgressChanged += () => { if (agg.InProgress == 0) running = false; else running = true; };
        agg.Aggregate();
        int lastProg = 0;
        float progStaySec = 0;
        while(running) {
          Thread.Sleep(100);
          if (agg.InProgress == lastProg) progStaySec += 0.1f;
          else {
            progStaySec = 0;
            lastProg = agg.InProgress;
          }
// Old code - this is a greatest historical value.
//          if (progStaySec > DOWNLOAD_TIMEOUT_SEC)
//          {
//            WebClientX.Interrupt();
//            agg.InProgress = 0;
//          }
        }
		//while(AggregatorMain.is_downloading_error){
		if(AggregatorMain.is_downloading_error){
			Console.WriteLine("WARNING: One or more items has the downloading error. Try to collect again...");
			//Running = true;
			//running = true;
			//agg.Aggregate();
		}

        Running = false;
		WebClientX.Interrupt();	//Interrupt here

		Console.WriteLine("Finished."); nbpack.NBPackMain.PostDatabase.ReadRefs_private(); //read refs and Flush() data, in the end.
		return;
      }
    }

    public class Aggregator
    {
        private const string UserAgentConfig = "useragent.config";
        private const string Downloaded = "downloaded.txt";
        private const string Places_txt = "places.txt";
        private const string Proxy_txt = "proxy.txt";
        private const string IP_Services_txt = "externalIPservices.txt";
        private const string ImgPattern = "href=\"[:A-z0-9/\\-\\.]*\\.png\"";

		public static bool 	only_RAM 							= 	true;
		public static bool 	save_files 							= 	false;
		public static bool 	do_not_save_and_do_not_delete 		= 	true;			//by default, do not delete anything, from "/download"-folder.
		public static int 	max_connections 					= 	6;
		
	  
        private int _inProgress = 0;
        public int InProgress
        { 
            get
            {
                return _inProgress;
            }

            set
            {
                if (value < 0) value = 0;
                _inProgress = value;
                ProgressChanged();
            }
        }

        public event Action ProgressChanged = delegate {};

        private static List<string> _places = new List<string>();		//empty list.
		private const string default_places = "# Put URLs to threads here, one per line:\n";

        private static List<string> proxies = new List<string>();		//empty list, because if this variable is unsigned, proxies.Count not working.
		private const string default_proxies =
			"# Syntax: each proxy per line [(http(s)://)+IP:PORT], where () is optional parameter.\n"+
			"# List of HTTP or HTTPS proxies:\n"						//see "/scripts/params.js"
		;

        private static List<string> IP_services = new List<string>();	//empty array
		private const string IP_services_default =
			"# List of the services, which returns external IP (need to check proxy connection).\n"+
			"# This services may return text, string, JSON, or HTML, contains external IP-address (IPv4).\n"
		;

        private readonly HashSet<string> _downloaded;

        private readonly WebHeaderCollection _headers;

	// Next three methods are calls from Program.cs too...
	//Begin checking and update params
		//Try read value from config, or set this from file, or from default value.
        public static void CheckUpdateProxyList()			//Update proxy-list, if this was been changed and specified in proxy.txt
        {
			//	Console.WriteLine("CheckUpdateProxyList");
			try{
				var proxies_list = Configurator.Instance.GetValue("Proxy_List", File.Exists(Proxy_txt)?File.ReadAllText(Proxy_txt):default_proxies);
				proxies = proxies_list.Split('\n').Where(p => (!p.StartsWith("#") && p!="")).ToList();
				if(File.Exists(Proxy_txt)){	//rename file if exists
					File.Move(Proxy_txt, "renamed_"+Proxy_txt);
				}
			}	
			catch (Exception ex){
				Console.WriteLine(ex);
			}	
			//	Console.WriteLine("CheckUpdateProxyList return; proxies.Count: "+proxies.Count.ToString());
			return;
        }

        public static void CheckUpdateIPServicesConfig()	//Update IP_services, if this was been changed and specified in externalIPservices.txt
        {
			//	Console.WriteLine("CheckUpdateIPServicesConfig");
			try{
				var IP_services_config = Configurator.Instance.GetValue("Services_Returns_External_IP", File.Exists(IP_Services_txt)?File.ReadAllText(IP_Services_txt):IP_services_default);
				IP_services = IP_services_config.Split('\n').Where(p => (!p.StartsWith("#") && p!="")).ToList();
				if(File.Exists(IP_Services_txt)){	//rename file if exists
					File.Move(IP_Services_txt, "renamed_"+IP_Services_txt);
				}
			}	
			catch (Exception ex){
				Console.WriteLine(ex);
			}	
			//	Console.WriteLine("CheckUpdateIPServicesConfig return; IP_services.Count: "+IP_services.Count.ToString());
			return;
        }

        public static void CheckUpdatePlacesConfig()		//Update threads, if this was been changed and specified in places.txt
        {
			//	Console.WriteLine("CheckUpdatePlacesConfig");
			try{
				var places = Configurator.Instance.GetValue("places", File.Exists(Places_txt)?File.ReadAllText(Places_txt):default_places);
				if(File.Exists(Places_txt)){	//rename file if exists
					File.Move(Places_txt, "renamed_"+Places_txt);
				}
				_places = places.Split('\n').Where(p => (!p.StartsWith("#") && p!="")).ToList();
			}	
			catch (Exception ex){
				Console.WriteLine(ex);
			}	
			//	Console.WriteLine("CheckUpdatePlacesConfig return; _places.Count: "+_places.Count.ToString());
			return;
        }
	//End of checking and update params

        public Aggregator(string [] _params_ = null)
        {
            try
            {
				if(_params_.Length!=0){
					for(int item=0; item<_params_.Length;item++)
					{
						//Console.WriteLine("_params_[i] = "+_params_[item]);
						if(_params_[item] == "collect_using_files"){
							Console.WriteLine("ParseImage: collect_using_files, only_RAM = false now");
							only_RAM = false;
						}else if(_params_[item] == "save_files"){
							Console.WriteLine("ParseImage: save_files save_files = true now");
							save_files = true;
						}
						else if(_params_[item] == "do_not_save_and_do_not_delete"){
							Console.WriteLine("ParseImage: do_not_save_and_do_not_delete, do_not_save_and_do_not_delete = true now");
							do_not_save_and_do_not_delete = true;
							save_files = false;
						}
						else if(_params_[item] == "delete_files"){
							Console.WriteLine("ParseImage: delete_files, save_files = false now");
							do_not_save_and_do_not_delete = false;
							save_files = false;
						}else if(_params_[item] == "collect_using_RAM"){
							Console.WriteLine("ParseImage: collect_using_RAM, only_RAM = true now");
							only_RAM = true;
						}else if(_params_[item] == "allowReput"){Console.WriteLine("Aggregator.cs: allowReput = true now"); nbpack.NBPackMain.allowReput = true;}else if(_params_[item] == "bypassValidation"){Console.WriteLine("Aggregator.cs: bypassValidation = true now"); nbpack.NBPackMain.bypassValidation = true;}
						else if(_params_[item].Contains("DownloadPNG: ")){
							Console.WriteLine("DownloadPNG: ");
							//return;
						}
						else{
							max_connections = nbpack.NBPackMain.parse_number(_params_[item]);
						}
					}
				}
				Console.WriteLine("only_RAM = "+only_RAM+", save_files = "+save_files+", max_connections = "+max_connections);

				//delete all files in "download" folder
				if(save_files==false && do_not_save_and_do_not_delete==false){
					if (!Directory.Exists("download"))
					{
						Directory.CreateDirectory("download");
					}
					Console.WriteLine("Delete files in \"download/\" folder...");
					System.IO.DirectoryInfo di = new DirectoryInfo("download"+Path.DirectorySeparatorChar);
					foreach (FileInfo file in di.GetFiles()){file.Delete();}									//this strings will clear all files and all folders in "/download"-folder !
					foreach (DirectoryInfo dir in di.GetDirectories()){dir.Delete(true);}

					// Create a file to write to "/download"-folder.
					using (StreamWriter sw = File.CreateText("download"+Path.DirectorySeparatorChar+"Files_will_deleted.txt")) 
					{
						sw.WriteLine("Files in this folder will be deleted, if option for saving this not specified in GET-query.");
						sw.WriteLine("Don't store any files here or enable \"save_files\". See the parameters of the query, in /scripts/init.js");
					}

				}

                if (!File.Exists(UserAgentConfig))
                {
                    File.WriteAllText(UserAgentConfig, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/42.0.2311.135 Safari/537.36 Edge/12.246");
                }

                string userAgent = File.ReadAllLines(UserAgentConfig).First(l => !l.StartsWith("#")).Trim();
                _headers = new WebHeaderCollection();
                _headers[HttpRequestHeader.UserAgent] = userAgent;
                _headers[HttpRequestHeader.Accept] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
                _headers[HttpRequestHeader.AcceptLanguage] = "en-US,en;q=0.8";
                _headers[HttpRequestHeader.CacheControl] = "max-age=0";

                if (File.Exists(Downloaded))
                {
                    _downloaded = new HashSet<string>(File.ReadAllLines(Downloaded));
                }
                else
                {
                    _downloaded = new HashSet<string>();
                }

			//	Console.WriteLine("Aggregator: CheckUpdatePlacesConfig()");
                CheckUpdatePlacesConfig();
			//	Console.WriteLine("Aggregator: CheckUpdateProxyList()");
				CheckUpdateProxyList();
			//	Console.WriteLine("Aggregator: CheckUpdateIPServicesConfig()");
				CheckUpdateIPServicesConfig();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error while creating containers aggregator:\n" + e.ToString());
            }
			return;
        }

        private bool IsUriValid(string uri)
        {
            try
            {
                var u = new Uri(uri);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Aggregate()
        {
            try
            {
			//	Console.WriteLine("Aggregate: CheckUpdatePlacesConfig();");
				CheckUpdatePlacesConfig();
			//	Console.WriteLine("Aggregate: CheckUpdateProxyList();");
				CheckUpdateProxyList();
			//	Console.WriteLine("Aggregate: CheckUpdateIPServicesConfig();");
				CheckUpdateIPServicesConfig();
                bool empty = true;

				try{
					if (!String.IsNullOrEmpty(proxy.Address.AbsoluteUri)) //check previous proxy
					{
						Console.WriteLine("Proxy URL: " + proxy.Address.AbsoluteUri+" was been used...");
						Console.WriteLine("Try to switch proxy...");
						proxy = AddProxy(new WebProxy());
					}
				}
				catch// (Exception ex)
				{
					//Console.WriteLine("Exception: "+ex);
					Console.WriteLine("Proxy not been used...");
					proxy = AddProxy(new WebProxy());
				}

				try{
					if (!String.IsNullOrEmpty(proxy.Address.AbsoluteUri))
					{
						Console.WriteLine("New proxy URL: " + proxy.Address.AbsoluteUri+" will be used...");
					}
				}
				catch// (Exception ex)
				{
					//Console.WriteLine("Exception: "+ex);
					Console.WriteLine("Proxy not used...");
				}

                foreach (string place in _places)
                {
                    if (!place.StartsWith("#"))
                    {
                        empty = false;

                        if (IsUriValid(place))
                        {
                            ParseText(place);
                        }
                    }
                }

                if (empty)
                {
					InProgress = 0;
                }
            }

            catch (Exception e)
            {
                Console.WriteLine("Error while parsing from places.txt:\n" + e.ToString());
            }
			return;
        }

/*
        private static void AddProxy(WebClientX client)
        {
            if (File.Exists(Proxy_txt))
            {
                //Console.WriteLine("PROXY USAGE IS DISABLED IN THIS VERSION (UNABLE TO SUPPORT PROXY FOR EVERY OCCASION)");
				var proxyUrl = File.ReadAllText(Proxy_txt);
                WebProxy proxy = new WebProxy();
                proxy.Address = new Uri(proxyUrl);
				//proxy.Credentials = new NetworkCredential("usernameHere", "pa****rdHere");  //These can be replaced by user input
				//proxy.UseDefaultCredentials = false;										//this false, in this case...
				//proxy.BypassProxyOnLocal = true;
				proxy.BypassProxyOnLocal = false;  					//still use the proxy for local addresses
                //client._wc.Proxy = proxy;							//WebClient, not WebclientX, working when _wc is public in WebClientX.cs
                client = WebClientX(proxy);						//update client, using extended method
            }
        }//+ PNGTransport/WebClientX.cs: "private WebClient _wc;" -> "public WebClient _wc;"
*/
		
		private static bool TryConnectProxy(string url) //get external IP and compare this with proxyIP
		{
			Uri uri = new Uri(url);
			string proxyIP = uri.Host;
			int proxyPort = uri.Port;

			foreach(string IPservice in IP_services){
				Console.WriteLine("Try to connect: "+IPservice);
				try{
					var req = (HttpWebRequest)HttpWebRequest.Create(IPservice);
					req.Timeout = 5000;
					req.Proxy = new WebProxy(proxyIP, proxyPort);
					string myip = null;
			
					// String where IP exists
					//String input = "var product_pic_fn=;var firmware_ver='20.02.024';var wan_ip='1992.75.120.206';if (parent.location.href != window.location.href)";
					// Regexp to extract IPv4 address
					Regex ip = new Regex(@"(?<First>2[0-4]\d|25[0-5]|[01]?\d\d?)\.(?<Second>2[0-4]\d|25[0-5]|[01]?\d\d?)\.(?<Third>2[0-4]\d|25[0-5]|[01]?\d\d?)\.(?<Fourth>2[0-4]\d|25[0-5]|[01]?\d\d?)");
					//MatchCollection result = ip.Matches(input);
					//Console.WriteLine((result[0]).ToString());	//show extracted IP-address (IPv4)

					using(var resp =   req.GetResponse()){
						string response = new StreamReader(resp.GetResponseStream()).ReadToEnd();	//json or text/string or html
						//Console.WriteLine("response"+response);
						MatchCollection result = ip.Matches(response);								//extract IP-address (IPv4)
						myip = (result[0]).ToString();												//result to string
						if (myip == proxyIP)	//if IP is the proxy IP
						{
							Console.WriteLine("CONNECT to "+IPservice+" - OK! IP: "+myip);
							//continue;
							return true;
						}else{
							Console.WriteLine("CONNECT to "+IPservice+" - OK,\nbut IP is not IP of the PROXY! IP: "+myip);
							continue;
						}
					}
				}catch{
					Console.WriteLine("CONNECT to "+IPservice+" - Fail!");
					continue;					
				}
			}

			Console.WriteLine("CONNECT to all IP services - Fail...\n\n");
			return false; //cann't get external IP for this proxy - connection to all IP-services was been failed.
		}

		private static bool TryPingProxy(string url)
		{
			try
			{
				Uri uri = new Uri(url);
				
				string proxyIP = uri.Host;
				int proxyPort = uri.Port;

				
				//HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create("http://www.google.com/404");
				//Try to open short 404 response. As variant, this can also be a small and popular - google.ico
				//but...
				HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);	//try to open this same proxy using itself.
				request.Timeout = 10000;												//with timeout
				request.Proxy = new WebProxy(url, true);								//append proxy
				request.AllowAutoRedirect = false;				// find out if this site is up and don't follow a redirector	//for HttpWebRequest
				request.Method = "HEAD";												//just head
				using (var response = request.GetResponse())	//dispose after
				{
					Console.WriteLine("PING - OK!");
					return TryConnectProxy(url);
				}
			}
			catch//(Exception ex)
			{
				//Console.WriteLine("catch: "+ex);
				return false;
			}
		}

        private static WebProxy AddProxy(WebProxy proxy_server)
        {
			CheckUpdateProxyList();
            proxy_server = new WebProxy();
            if (proxies.Count!=0)
            {
                CheckUpdateIPServicesConfig();	//get list of IP_services to get external IP of the proxies, or set this list as default
				
				string proxyUrl = "";
				proxies = proxies.OrderBy(a => Guid.NewGuid()).ToList();									//RANDOMIZE PROXY-LIST
				
				int proxy_index = 0;
				string proxy_URL = "";
				
				while(true) {
					if(proxy_index>=proxies.Count || proxies[proxy_index]==""){
						proxyUrl = "";
						break;
					}
					proxy_URL = proxies[proxy_index];
					
					if(!proxy_URL.Contains("http://") && !proxy_URL.Contains("https://")){
						proxies.Add("http://"+proxy_URL);	//add http
						proxies.Add("https://"+proxy_URL);	//and https
					}else{
						Console.WriteLine("Try ping proxy: "+proxy_URL);
						if(TryPingProxy(proxy_URL)==true){	//ping and connect to proxy
						
							proxyUrl = proxy_URL;
						
							Console.WriteLine("Selected Proxy: "+proxyUrl);
							proxy_server.Address = new Uri(proxyUrl);
							//proxy_server.Credentials = new NetworkCredential("usernameHere", "pa****rdHere");  //These can be replaced by user input
							//proxy_server.UseDefaultCredentials = false;										//this false, in this case...
							//proxy_server.BypassProxyOnLocal = true;
							proxy_server.BypassProxyOnLocal = false;  					//still use the proxy for local addresses
							//client._wc.Proxy = proxy_server;							//WebClient, not WebclientX, working when _wc is public in WebClientX.cs
							//client = WebClientX(proxy_server);						//update client, using extended method
						
							break;
						
						}else{
							proxyUrl = "";
						}
					}
					proxy_index++;
				}
				if(proxyUrl == ""){Console.WriteLine("No responce from any proxy.");}
            }
			
			try{
				if (!String.IsNullOrEmpty(proxy_server.Address.AbsoluteUri))
				{
					Console.WriteLine("Proxy URL: " + proxy_server.Address.AbsoluteUri + " was selected as current proxy.");
					if(!AggregatorMain.is_already_increased_if_proxy){
						AggregatorMain.maximum_timeout += 30000;			//just +30 sec to maximum timeout, if proxy used.
						AggregatorMain.is_already_increased_if_proxy = true;
					}
				}
			}
			catch// (Exception ex)
			{
				//Console.WriteLine("Exception: "+ex);
				Console.WriteLine("Do not using proxy...");
			}
			
			return proxy_server;
        }//+ PNGTransport/WebClientX.cs: "private WebClient _wc;" -> "public WebClient _wc;"

		private static WebProxy proxy = new WebProxy();
		//and change everywhere in this file:

		//var client = new WebClientX();
		//to var client = new WebClientX(proxy);

        private void ParseText(string address)
        {
            var client = new WebClientX(proxy);
            //AddProxy(client);
            client.Headers = _headers;
			
			bool downloaded_thread = false;	//true, when thread downloaded.

			//calculate host once from address, not for each picture
			//this code calculating host with folder for images relative pathways.
			//		__BEGIN__
			string[] items = address.Split('/');	//split address from settings by slash
			string host_and_folder = "";			//empty string
			string item = "";						//empty temp item
			int len = items.Length;					//array length
			int last = len-1;						//last element index.
			string host = "";						//host (IP:PORT only)
			string picture_host = "";				//temp empty variable to save picture host.
			string protocol_host_port = "";         //protocol://(domain/IP):PORT - without slash in the end
			
			for(int i = 0; i<len; i++){				//check all items
//				Console.WriteLine("item: {0}, value: '{1}'", i, items[i]);	//show item, value
				item = items[i];											//write item to temp string

				if(
						(i==0 && item.IndexOf(".")!=-1)						//if first item contains dot ("www.domain.com:1234/folder")
					|| 	i==2												//or if this is next item, after protocol
				){
					host = item;											//save this item as host.
				}

				if(
						i!=last												//if this is not last item
					|| 	(i==2 && len==3)									//or if domain/IP(+port) only, without slash
				){
					item = item+'/';										//add slash
				}
				if(
						(len!=3 && i==last)									//if last item
					&& 	item.IndexOf('.')!=-1								//and if dot found
				){//maybe, this is filename. If not - add slash in the end...
					//do not add this file to path
				}else{
					host_and_folder = String.Concat(host_and_folder, item);	//if not last item or if dot not found, add this as folder.
				}
			}
            protocol_host_port = Regex.Match(address, @"^(?<proto>\w+)://+?(?<host>[A-Za-z0-9\-\.]+)+?(?<port>:\d+)?/", RegexOptions.None).Result("${proto}://${host}${port}");
			//		__END__

            client.DownloadDataCompleted += bytes => 
            {
                if (bytes == null) {
                    Console.WriteLine("Null bytes received from " + address + ". Maybe connection is bad.");
                    return;
                }else{
					downloaded_thread = true;
				}
                Console.WriteLine("Thread download (FINISH): " + address);
                NotificationHandler.Instance.Messages.Enqueue("Thread downloaded: " + address);
                //_downloaded.Add(address);

                string imageAddress = "";
                try
                {
                    string text = Encoding.UTF8.GetString(bytes);
					//Console.WriteLine("Downloaded data (partial): "+text.Substring(0, (text.Length>40)?40:text.Length)); //It was been received HTML or some another bullshit?
					if(text.Contains("Bad Request")){
						Console.WriteLine("Error - \"Bad request\" response for: "+address);
						AggregatorMain.is_downloading_error = true;
					}
                    //string host = Regex.Match(address, "https?://[A-z\\.0-9-]*").Value;	//original code, commented.
					//return protocol://(IP/domain) only, without PORT, folder, and slash.

                    var images = Regex.Matches(text, ImgPattern);

					//Console.WriteLine("parseText() - images.Length = "+images.Count);
                    foreach (Match im in images)
                    {
                        imageAddress = im.Value.Replace("href=", "").Trim('"');

                        if (imageAddress.Contains("http://") || imageAddress.Contains("https://"))//if link in href not relative and contains "http://" or "https://"
                        {
							//get host for picture
							//Warning, IP-loggers can working on another PORTS and PROTOCOLS!

							//picture_host = Regex.Match(imageAddress, "[^https?://][A-z\\.0-9:0-9]*").Value; //host+port - without protocol

							//another regular expressions:
								//protocol://(ip/domain):port
							//picture_host = Regex.Match(imageAddress, @"^(?<proto>\w+)://+?(?<host>[A-Za-z0-9\-\.]+)+?(?<port>:\d+)?/", RegexOptions.None, TimeSpan.FromMilliseconds(150)).Result("${proto}://${host}${port}");
								//(ip/domain):port only - without protocol.
							picture_host = Regex.Match(imageAddress, @"^(?<proto>\w+)://+?(?<host>[A-Za-z0-9\-\.]+)+?(?<port>:\d+)?/", RegexOptions.None).Result("${host}${port}");
								//If HTTPS board loading images from HTTP host no any difference in protocol...
								//But... IP-loggers on another protocol not excluded.

							//Console.WriteLine("---> picture_host:{0}", picture_host); //just write this
						}
						/*
						else if("ftp://" and another protocols...){}//do something...
						*/
						else if(imageAddress.Contains("://")){//if not http or https, but something://blah-blah...
							Console.WriteLine("Image  download starting: {0}\nUnknown protocol.",imageAddress); //just write this
							_downloaded.Add(imageAddress);
							continue;
						}else if(imageAddress[0]=='/'){										//if relative path "/img/pic.png"
							imageAddress = protocol_host_port+imageAddress;						//add to protocol_host_port
						}else if(imageAddress[0]=='.' && imageAddress[1]=='/'){				//if relative path "./img/pic.png"
							imageAddress = protocol_host_port+imageAddress.Substring(1); 		//add to protocol_host_port without dot.
						}else{
                            imageAddress = host_and_folder + imageAddress; 						//add relative pathway for picture, to the board host_and_folder.
                        }

                        if (IsUriValid(imageAddress))
                        {
                            //if (InProgress > 16)
                            while (InProgress >= max_connections)
                            //if (InProgress > 4)
                            {
                                Thread.Sleep(4000);
                            }
							picture_host = Regex.Match(imageAddress, @"^(?<proto>\w+)://+?(?<host>[A-Za-z0-9\-\.]+)+?(?<port>:\d+)?/", RegexOptions.None).Result("${host}${port}");

							//Console.WriteLine("address: "+address+"\nimageAddress: "+imageAddress+"\n"); //show image address and picture address.
                            
							//IP-logging attempt prevention
							if (
									imageAddress.Contains("logger")		//iploggers by keyword "logger"
								||	imageAddress.Contains("bit.ly")		//some url shorters
								||	imageAddress.Contains("goo.gl")
							)
							{
								Console.WriteLine(	"Image download starting: {0}\n"+	//show imageURL
													"IP-Logging attempt prevented: {0}",
													imageAddress
								);
								//and do nothing...
								_downloaded.Add(imageAddress);
							}else if( //block all pictures from another hosts, if...
									host_and_folder.IndexOf(picture_host)==-1				//picture_host not founded in host_and_folder
									//(https://someIPLOGGER.com/collectIP.png) loading from (https://wowchan.net/thread/res/1565/?search=png&timesorting=50)
									//true in this case								
								&&	picture_host.IndexOf(host)==-1
									//and if host not founded in picture_host (https://media.8ch.net/folder/pic.png loaded from https://8ch.net/)
									//false in this case, and true in prefious case.
							){
								//don't download this picture.
								//show message in console:
								Console.WriteLine(
													"Image download starting: {0}\n"+
													"IP-log block: picHost('{1}') != boardHost('{2}')",
													imageAddress,
													picture_host,
													host
													//host is the part of host_and_folder and this must be founded (host_and_folder.indexOf(host)!=-1)
								);
								_downloaded.Add(imageAddress);
								//and do nothing...
							}else{//else, download and parse image from this imageAddress.
							
								//Console.WriteLine("InProgress: "+InProgress);	//<(max_connections+1) - good.
								
								while(
									GC.GetTotalMemory(true)								//true, means try to collect garbage, before calculating total memory used.
									>
									AggregatorMain.maximum_collect_memory_limit_to_wait	//default value is 200 MBytes, but this can be customized.
								){
									//Console.WriteLine(GC.GetTotalMemory(false)+" memory used over limit "+AggregatorMain.maximum_collect_memory_limit_to_wait+". Wait 0.5 second for "+imageAddress+"...");
									System.Threading.Thread.Sleep(500);
								}
			
								ParseImage(imageAddress);
								_downloaded.Add(imageAddress);
							}
                        }else{
							Console.WriteLine("Invalid URI: "+imageAddress);
							_downloaded.Add(imageAddress);
						}
                    }
                }

                catch (Exception ex)
                {
                    Console.WriteLine("Error downloading image:" + imageAddress);
//                    if (e.Error != null)
//                        Console.WriteLine(e.Error.Message);
                    Console.WriteLine(ex.Message);
                }
                InProgress -= 1;
//                NotificationHandler.Instance.Messages.Enqueue(InProgress + " connections opened.");
            };

			
            while (InProgress >= max_connections)
            {
				Thread.Sleep(4000);
            }
            
			InProgress += 1;
            Console.WriteLine("Thread download starting: " + address);
            client.DownloadDataAsync(new Uri(address));
			
			System.Timers.Timer runonce=new System.Timers.Timer(AggregatorMain.maximum_timeout);	//timeout to download HTML code of thread.
			runonce.Elapsed+=(s, e) => {
				if(downloaded_thread == false){
					string stop_notif = "Thread download Time OUT ("+(AggregatorMain.maximum_timeout/1000)+" sec): "+address;
					Console.WriteLine(stop_notif);
					NotificationHandler.Instance.Messages.Enqueue(stop_notif);

					client.CancelAsync();	//cancel downloading
					InProgress -= 1;		//and delete from progress.
					AggregatorMain.is_downloading_error = true;
					return;
				}
			};
			runonce.AutoReset=false;
			runonce.Start();
			
        }
		
        public string DownloadPNG(string[] params_, bool is_lite = false)
		{
			string URL = "";
			int i = 0, interval = 500, max_interval = 5000;	//milliseconds
			bool downloadFile = false;
			
			for(i=0;i<params_.Length;i++){
				if(params_[i].Contains("DownloadPNG: ")){
					URL = params_[0].Split(new string [] {"DownloadPNG: "}, StringSplitOptions.None)[1];
				}else if(params_[i].Contains("DownloadFile: ")){
					URL = params_[0].Split(new string [] {"DownloadFile: "}, StringSplitOptions.None)[1];
					downloadFile = true;
				}else if(i==1){
					interval = nbpack.NBPackMain.parse_number(params_[1]);
				}else if(i==2){
					max_interval = nbpack.NBPackMain.parse_number(params_[2]);
				}else{
					return "invalid arguments. string format: \"DownloadPNG: URL|interval|max_interval\"";
				}
			}
			var client = new WebClientX(proxy);
            client.Headers = _headers;
			
			string downloaded_from_URL = "false";
			string download_to_folder = ((is_lite == true) ? "download"+Path.DirectorySeparatorChar+"created" : "containers");
			if (!Directory.Exists("temp")) Directory.CreateDirectory("temp");
			if (!Directory.Exists("download")) Directory.CreateDirectory("download");
			if (!Directory.Exists(download_to_folder)) Directory.CreateDirectory(download_to_folder);
			if( downloadFile == true )
			{
/*
				string remoteUri = URL;
				string fileName = "ms-banner.gif", myStringWebResource = null;
				// Create a new WebClient instance.
				WebClient myWebClient = new WebClient();
				// Concatenate the domain with the Web resource filename.
*/
				string [] splitURL = URL.Split('/');
				string fileName = download_to_folder + Path.DirectorySeparatorChar + splitURL[splitURL.Length-1], myStringWebResource = null;
				myStringWebResource = URL;
				Console.WriteLine("Downloading File \"{0}\" from \"{1}\" .......\n\n", fileName, myStringWebResource);
				// Download the Web resource and save it into the current filesystem folder.
				client.DownloadFile(new Uri(myStringWebResource),fileName);		
				Console.WriteLine("Successfully Downloaded File \"{0}\" from \"{1}\"", fileName, myStringWebResource);
				string response = "\nDownloaded file saved in the following file system folder:\n\t" + fileName;
				Console.WriteLine(response);
				downloadFile = false;
				return response;
			}
			else{
				client.DownloadDataCompleted += bytes => 
				{
						MemoryStream ms = null;
						Image RAM_container = null;
					
						string [] temp = URL.Split('/');
						string name = temp[temp.Length-1];
						if(only_RAM==false){
							File.WriteAllBytes("temp" + Path.DirectorySeparatorChar + name, bytes);
							File.Move("temp" + Path.DirectorySeparatorChar + name, download_to_folder + Path.DirectorySeparatorChar + name);
							if(save_files==true){
								Console.WriteLine("\n"+URL+"\nsaved as "+download_to_folder + Path.DirectorySeparatorChar + name+"\n");
							}
						}
						else{
							ms = new MemoryStream(bytes);
							RAM_container = Image.FromStream(ms);														//image in RAM
							if(save_files==true){
								File.WriteAllBytes( download_to_folder + Path.DirectorySeparatorChar + name, ms.ToArray());
								Console.WriteLine("\n"+URL+"\nsaved as "+name+"\n");
							}
						}
						//GC.Collect();
						Console.WriteLine("Image  download (FINISH): " + URL);
					
						string filepath = 
								name.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)								//file pathway
						;
						downloaded_from_URL = 
								"Image Downloaded from " + URL
							+	"<br>"
							+	"<a href=\""
							+	"../"
							+	(download_to_folder + Path.DirectorySeparatorChar + filepath)
							+"\" download=\""+name.Replace("download\\", "")+"\">"
							+		"<img src=\"../"+(download_to_folder + Path.DirectorySeparatorChar + filepath)+"\"/>"
							+		"<br>"+name.Replace("download\\", "")
							+	"</a>"
						;
					return;
				};
				client.DownloadDataAsync(new System.Uri(URL));
	
				i = 0;
				do{
					Thread.Sleep(interval);
					i+=interval;
				}
				while( (downloaded_from_URL == "false") && (i < max_interval) );
			
				return downloaded_from_URL;
			}
		}

        private void ParseImage(string address)
        {
			if (_downloaded.Contains(address)){
                //Console.WriteLine("downloaded.txt contains {0}", address);
				return;
			}
			
			bool image_downloaded = false;
            
//			using(var client = new WebClient()){
//				client.Proxy = proxy;
			using(var client = new WebClientX(proxy)){
			
				client.Headers = _headers;
				
				while (InProgress >= max_connections)
				{
					Thread.Sleep(4000);
				}
            
				InProgress += 1;
			
				//Replace images URLs:
				address = address.Replace("2ch.hk", "m2ch.hk");			//m2ch.hk working. See also the exception at line 265 with condition (picture_host!="2ch.hk" && host!="m2ch.hk")
				address = address.Replace("mm2ch.hk", "m2ch.hk");		//m2ch.hk contains 2ch.hk, and replaced to mm2ch.hk. Turn it back.
				address = ( address.IndexOf("volgach") != -1 ) ? address.Replace("https", "http") : address;	//using http for volgach.ru
			
				Console.WriteLine("Image  download starting: " + address);
				//client.DownloadDataAsync(new Uri(address));

				System.Timers.Timer runonce=new System.Timers.Timer(AggregatorMain.maximum_timeout);	//timeout to download image
				runonce.Elapsed+=(s, e) => {
					if(image_downloaded == false){
						string stop_notif = "Image  download Time OUT ("+(AggregatorMain.maximum_timeout/1000)+" sec): "+address;
						Console.WriteLine(stop_notif);
						NotificationHandler.Instance.Messages.Enqueue(stop_notif);

						client.CancelAsync();			//cancel download
						InProgress -= 1;				//and delete from progress.
						AggregatorMain.is_downloading_error = true;
						return;
					}
				};
				runonce.AutoReset=false;
				runonce.Start();

				byte[] bytes = client.DownloadData(address);

				if (bytes == null) {
                    Console.WriteLine("Ignoring null bytes. Maybe connection is bad.");
                    return;
                }else{
					image_downloaded = true;
				}
				
                try
                {
                    if (!Directory.Exists("temp"))
                    {
                        Directory.CreateDirectory("temp");
                    }

                    if (!Directory.Exists("download"))
                    {
                        Directory.CreateDirectory("download");
                    }

                    MemoryStream ms = null;
					Image RAM_container = null;
					string name = null;

					if(only_RAM==false){
						//Old code. 																				file pathway
						name = Guid.NewGuid().ToString().Trim('{', '}')+".png";
						File.WriteAllBytes("temp" + Path.DirectorySeparatorChar + name, bytes);
						File.Move("temp" + Path.DirectorySeparatorChar + name, "download" + Path.DirectorySeparatorChar + name);
						if(save_files==true){
							Console.WriteLine("\n"+address+"\nsaved as "+"download" + Path.DirectorySeparatorChar + name+"\n");
						}
                    }
					else{
						ms = new MemoryStream(bytes);
						RAM_container = Image.FromStream(ms);														//image in RAM
						if(save_files==true){
							var name_ms = "download" + Path.DirectorySeparatorChar + Guid.NewGuid().ToString().Trim('{', '}')+".png";
							File.WriteAllBytes(name_ms, ms.ToArray());
							Console.WriteLine("\n"+address+"\nsaved as "+name_ms+"\n");
						}
					}
					//Console.WriteLine("only_RAM = "+only_RAM+", save_files = "+save_files+", max_connections = "+max_connections);
					
					//GC.Collect();
                    
					try{
						Console.WriteLine("Image  download (FINISH): " + address);
						NotificationHandler.Instance.Messages.Enqueue("Image downloaded: " + address);
					}
					catch(Exception ex){
						Console.WriteLine("Aggregator.cs - ParseImage: Try to add notif: "+ex+"\n address: "+address+"\n\n");
					}

					try{
						//client.CancelAsync();			//cancel download
						//client.Dispose();				//Dispose WebClient

						//GC.Collect();
						//GC.WaitForPendingFinalizers();
						//GC.Collect();

						//long usedMemory = GC.GetTotalMemory(false);
						//Console.WriteLine("Memory used: "+usedMemory);	//Not so much, like in following method ParseImage2

						if (only_RAM==true){
							nbpack.NBPackMain.ParseFile("http://" 
								+ Configurator.Instance.GetValue("ip", "127.0.0.1") 
								+ ":"
								+ Configurator.Instance.GetValue("port", "7346"),
								Configurator.Instance.GetValue("password", Configurator.DefaultPass),
								RAM_container																//Image in RAM
							);
						}else{
							nbpack.NBPackMain.ParseFile("http://" 
								+ Configurator.Instance.GetValue("ip", "127.0.0.1") 
								+ ":"
								+ Configurator.Instance.GetValue("port", "7346"),
								Configurator.Instance.GetValue("password", Configurator.DefaultPass),
								"download" + Path.DirectorySeparatorChar + name								//file pathway
								, save_files
							);						
						}
					}
					catch(Exception ex){
						Console.WriteLine("Aggregator.cs - parseImage: Try to parseFile: "+ex);
					}

					try{
						if(only_RAM!=false){
							ms.Dispose();
							RAM_container.Dispose();																		//Image in RAM. Try flush RAM, after parsing.
						}
					}
					catch(Exception ex){
						Console.WriteLine("Aggregator.cs - ParseImage: Try to dispose: "+ex);
					}
					
                    try
                    {
                        NDB.FileUtil.Append(Downloaded, address + "\n");
                    }
                    catch
                    {
						Console.WriteLine("Can't append to downloaded.");
                        System.Threading.Thread.Sleep(1000);

                        try
                        {
                            NDB.FileUtil.Append(Downloaded, address + "\n");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("downloaded.txt appending error:\n" + e.ToString());
                        }
                    }
                }

                catch (Exception ex)
                {
                    Console.WriteLine("Error downloading url: " + address);
//                    if (e.Error != null)
//                        Console.WriteLine(e.Error.Message);
                    Console.WriteLine("Exception: "+ex.Message+", InProgress: "+InProgress);
                }
                InProgress -= 1;
//                NotificationHandler.Instance.Messages.Enqueue(InProgress + " connections opened.");
				return;
			}
		}

		//old code + modifications. Just as history, and maybe to work with this in future:
        private void ParseImage2(string address)
        {
            if (_downloaded.Contains(address)){
                //Console.WriteLine("downloaded.txt contains {0}", address);
				return;
			}
            var client = new WebClientX(proxy);
            //AddProxy(client);
            client.Headers = _headers;

			bool image_downloaded = false;
            client.DownloadDataCompleted += bytes => 
            {
                if (bytes == null) {
                    Console.WriteLine("Ignoring null bytes");
                    return;
                }else{
					image_downloaded = true;
				}
				
                try
                {
                    if (!Directory.Exists("temp"))
                    {
                        Directory.CreateDirectory("temp");
                    }

                    if (!Directory.Exists("download"))
                    {
                        Directory.CreateDirectory("download");
                    }

                    MemoryStream ms = null;
					Image RAM_container = null;
					string name = null;
					if(only_RAM==false){
						//Old code. 																				file pathway
						name = Guid.NewGuid().ToString().Trim('{', '}')+".png";
						File.WriteAllBytes("temp" + Path.DirectorySeparatorChar + name, bytes);
						File.Move("temp" + Path.DirectorySeparatorChar + name, "download" + Path.DirectorySeparatorChar + name);
						if(save_files==true){
							Console.WriteLine("\n"+address+"\nsaved as "+"download" + Path.DirectorySeparatorChar + name+"\n");
						}
                    }
					else{
						try{
							ms = new MemoryStream(bytes);
							RAM_container = Image.FromStream(ms);														//image in RAM
						}
						catch{
						//	(Exception ex){	//exception, when not image bytes received from address.
						//		Console.WriteLine("Invalid image bytes received from address: "+address+"\nException: "+ex);	//don't show this
								return;	//and just return.
						}
						if(save_files==true){
							var name_ms = "download" + Path.DirectorySeparatorChar + Guid.NewGuid().ToString().Trim('{', '}')+".png";
							File.WriteAllBytes(name_ms, ms.ToArray());
							Console.WriteLine("\n"+address+"\nsaved as "+name_ms+"\n");
						}
					}
					//Console.WriteLine("only_RAM = "+only_RAM+", save_files = "+save_files+", max_connections = "+max_connections);
					
					//GC.Collect();
                    
					try{
						Console.WriteLine("Image  download (FINISH): " + address);
						NotificationHandler.Instance.Messages.Enqueue("Image downloaded: " + address);
					}
					catch(Exception ex){
						Console.WriteLine("Aggregator.cs - ParseImage: Try to add notif: "+ex+"\n address: "+address+"\n\n");
					}

					try{
//commented parsing of image.
/*
						if (only_RAM==true){
							nbpack.NBPackMain.ParseFile("http://" 
								+ Configurator.Instance.GetValue("ip", "127.0.0.1") 
								+ ":"
								+ Configurator.Instance.GetValue("port", "7346"),
								Configurator.Instance.GetValue("password", Configurator.DefaultPass),
								RAM_container																//Image in RAM
							);
						}else{
							nbpack.NBPackMain.ParseFile("http://" 
								+ Configurator.Instance.GetValue("ip", "127.0.0.1") 
								+ ":"
								+ Configurator.Instance.GetValue("port", "7346"),
								Configurator.Instance.GetValue("password", Configurator.DefaultPass),
								"download" + Path.DirectorySeparatorChar + name								//file pathway
								, save_files
							);						
						}
*/
						long usedMemory = GC.GetTotalMemory(false);
						Console.WriteLine("ParseFile commented. Memory used: "+usedMemory);
						//This eating many Random Access Memory, even when parsing is commented, and this memory usage is growing...
						//Problem somewhere in WebClientX.
						//Made this IDisposable, and runned this with DownloadData in "using(webClientX){}"
					}
					catch(Exception ex){
						Console.WriteLine("Aggregator.cs - parseImage: Try to parseFile: "+ex);
					}

					try{
						if(only_RAM!=false){
							ms.Dispose();
							RAM_container.Dispose();																		//Image in RAM. Try flush RAM, after parsing.
						}
					}
					catch(Exception ex){
						Console.WriteLine("Aggregator.cs - ParseImage: Try to dispose: "+ex);
					}
					
                    //_downloaded.Add(address);
                    
                    try
                    {
                        NDB.FileUtil.Append(Downloaded, address + "\n");
                    }
                    catch
                    {
						Console.WriteLine("Cannt append to downloaded.");
                        System.Threading.Thread.Sleep(1000);

                        try
                        {
                            NDB.FileUtil.Append(Downloaded, address + "\n");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("downloaded.txt appending error:\n" + e.ToString());
                        }
                    }
                }

                catch (Exception ex)
                {
                    Console.WriteLine("Error downloading url: " + address);
//                    if (e.Error != null)
//                        Console.WriteLine(e.Error.Message);
                    Console.WriteLine("Exception: "+ex.Message+", InProgress: "+InProgress);
                }
                InProgress -= 1;
//                NotificationHandler.Instance.Messages.Enqueue(InProgress + " connections opened.");
            };

			
            while (InProgress >= max_connections)
            {
				Thread.Sleep(4000);
            }
            
			InProgress += 1;
			
			//Replace images URLs:
            address = address.Replace("2ch.hk", "m2ch.hk");			//m2ch.hk working. See also the exception at line 265 with condition (picture_host!="2ch.hk" && host!="m2ch.hk")
            address = address.Replace("mm2ch.hk", "m2ch.hk");		//m2ch.hk contains 2ch.hk, and replaced to mm2ch.hk. Turn it back.
            address = ( address.IndexOf("volgach") != -1 ) ? address.Replace("https", "http") : address;	//using http for volgach.ru
			
            Console.WriteLine("Image  download starting: " + address);
            client.DownloadDataAsync(new Uri(address));
			
			System.Timers.Timer runonce=new System.Timers.Timer(AggregatorMain.maximum_timeout);	//timeout to download image
			runonce.Elapsed+=(s, e) => {
				if(image_downloaded == false){
					string stop_notif = "Image  download Time OUT ("+(AggregatorMain.maximum_timeout/1000)+" sec): "+address;
					Console.WriteLine(stop_notif);
					NotificationHandler.Instance.Messages.Enqueue(stop_notif);

					client.CancelAsync();			//cancel download
					InProgress -= 1;				//and delete from progress.
					AggregatorMain.is_downloading_error = true;
					return;
				}
			};
			runonce.AutoReset=false;
			runonce.Start();
			
			return;
        }
    }
}
