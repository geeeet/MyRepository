/****************************************************
	文件：PESocket.cs
	作者：Plane
	邮箱: 1785275942@qq.com
	日期：2018/10/30 11:20   	
	功能：PESocekt核心类
*****************************************************/

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
/***
        where 子句还可以包括构造函数约束。
        可以使用 new 运算符创建类型参数的实例；但类型参数为此必须受构造函数约束 new() 的约束。new() 约束可以让编译器知道：提供的任何类型参数都必须具有可访问的无参数（或默认）构造函数。例如：
        public class MyGenericClass<T> where T : IComparable, new()
        {
            // The following line is not possible without new() constraint:
            T item = new T();
        }
        new() 约束出现在 where 子句的最后。
***/
namespace PENet
{
    public class PESocket<T, K>
        //泛型约束为T继承自PESession<K>,并必须要有无返回值无参数的构造函数
        where T : PESession<K>, new()
        where K : PEMsg
    {
        //Socket引用自System.Net.Sockets
        private Socket skt = null;
        public T session = null;
        public int backlog = 10;
        List<T> sessionLst = new List<T>();

        /// <summary>
        /// 标准的无参数构造函数，构造函数中自动指定了一个地址为Ipv4，SocketType为Stream，ProtocolType为TCP的Socket连接
        /// </summary>
        public PESocket()
        {
            skt = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        #region Server
        /// <summary>
        /// Launch Server,根据传入的ip和port（端口号）来开启服务
        /// </summary>
        public void StartAsServer(string ip, int port)
        {
            //使用try catch语句捕捉异常防止服务器出错宕机
            try
            {
                //绑定服务器Socket的IpEndPoint(ip,port)，使服务器在该ip和port上建立通信，客户端连接这个ipEndPort就可以与服务端通信
                skt.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
                //设定服务器端最大的连接队列数（这个数值的意义尚未弄明白）
                skt.Listen(backlog);
                //开始等待客户端的连接，如果连接成功，则调用回调函数，并且将服务器Socket作为参数传递进去
                skt.BeginAccept(new AsyncCallback(ClientConnectCB), skt);
                PETool.LogMsg("\nServer Start Success!\nWaiting for Connecting......", LogLevel.Info);
            }
            catch (Exception e)
            {
                PETool.LogMsg(e.Message, LogLevel.Error);
            }
        }

        void ClientConnectCB(IAsyncResult ar)
        {
            try
            {
                //结束连接,返回的Socket其实是连接上客户端的Socket，即这个Socket可以访问到客户端的数据
                Socket clientSkt = skt.EndAccept(ar);
                T session = new T();
                //开始接受数据
                session.StartRcvData(clientSkt, () =>
                {
                    //如果在List中已经包含了该会话，则将该会话移除
                    if (sessionLst.Contains(session))
                    {
                        sessionLst.Remove(session);
                    }
                });
                //将该会话加入到list中
                sessionLst.Add(session);
            }
            catch (Exception e)
            {
                PETool.LogMsg(e.Message, LogLevel.Error);
            }
            //再次开启以接收下一次的客户端访问
            skt.BeginAccept(new AsyncCallback(ClientConnectCB), skt);
        }
        #endregion

        #region Client
        /// <summary>
        /// Launch Client
        /// </summary>
        public void StartAsClient(string ip, int port)
        {
            try
            {
                //与服务器端的BeginAccept对应，连接到服务器的IPEndPoint,并传入自身Socket
                skt.BeginConnect(new IPEndPoint(IPAddress.Parse(ip), port), new AsyncCallback(ServerConnectCB), skt);
                PETool.LogMsg("\n服务器启动成功了喵~~~\n欢迎连接到时空枢纽喵......", LogLevel.Info);
            }
            catch (Exception e)
            {
                PETool.LogMsg(e.Message, LogLevel.Error);
            }
        }
        /// <summary>
        /// 客户端连接至服务器成功的回调函数
        /// </summary>
        /// <param name="ar"></param>
        void ServerConnectCB(IAsyncResult ar)
        {
            try
            {
                //结束掉连接，连接的信息都存入了返回的IAsyncResult中
                skt.EndConnect(ar);
                session = new T();
                //准备接受数据
                session.StartRcvData(skt, null);
            }
            catch (Exception e)
            {
                PETool.LogMsg(e.Message, LogLevel.Error);
            }
        }
        #endregion
        /// <summary>
        /// 关闭Socket，结束连接操作
        /// </summary>
        public void Close()
        {
            if (skt != null)
            {
                skt.Close();
            }
        }

        /// <summary>
        /// 设置PETool中的Log状态
        /// </summary>
        /// <param name="log">log switch</param>
        /// <param name="logCB">log function</param>
        public void SetLog(bool log = true, Action<string, int> logCB = null)
        {
            if (log == false)
            {
                PETool.log = false;
            }
            if (logCB != null)
            {
                PETool.logCB = logCB;
            }
        }
    }
}