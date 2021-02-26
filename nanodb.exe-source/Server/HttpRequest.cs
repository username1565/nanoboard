﻿using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Net.Sockets;
using System.Net;

namespace NServer
{
    /*
        Parses HTTP request from HTTP connection, extracts method, address, headers and content
    */
    class HttpRequest
    {
        public readonly string Method;
        public readonly string Address;
        public readonly string Host;
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();
        public readonly string Content;
        public readonly HttpConnection Connection;
        public readonly bool Invalid = false;

        public HttpRequest(HttpConnection conn, string request)
        {
            Connection = conn;
            if (string.IsNullOrEmpty(request))
            {
                Invalid = true;
                return;
            }

            var fls = request.Split(new char[]{ ' ' }, 4);

            if (fls.Length < 3)
            {
                Invalid = true;
                return;
            }

            Method 		= 	fls[0];
            Address 	= 	fls[1];
            Host 		= 	((fls[3]).Split(new string[]{ "\r\n" }, 2, StringSplitOptions.None))[0];

            var heco = request.Split(new string[]{ "\r\n\r\n" }, 2, StringSplitOptions.None);

            if (heco.Length < 2)	//DON'T CHANGE THIS VALUE, because params in requests will not be transferred correctly.
            {
                Invalid = true;
                return;
            }

            var he = heco[0];
            var co = heco[1];
            Content = co;
            var lines = he.Split(new string[]{ "\r\n", "\n" }, StringSplitOptions.None);

            for (int i = 1; i < lines.Length; i++)
            {
                var hl = lines[i].Split(new String[]{": "}, 2, StringSplitOptions.None);
                if (hl.Length < 2) continue;
                Headers[hl[0]] = hl[1];
            }
//			Console.WriteLine("Method: "+Method+", Address: "+Address+", Host: "+Host+", Content: "+Content+", Invalid: "+Invalid );
        }
    }
}
