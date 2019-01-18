/****************************************************
	文件：PESession.cs
	作者：Plane
	邮箱: 1785275942@qq.com
	日期：2018/10/30 11:20   	
	功能：网络会话管理
*****************************************************/

using System;
using System.Net.Sockets;

namespace PENet {
    public abstract class PESession<T> where T : PEMsg
    {
        //skt为创建的服务器或客户端连接的Socket
        private Socket skt;
        //closeCB为关闭连接时调用的回调函数
        private Action closeCB;

        #region Recevie
        /// <summary>
        /// 接受数据函数，传入连接Socket和要调用的
        /// </summary>
        /// <param name="skt"></param>
        /// <param name="closeCB"></param>
        public void StartRcvData(Socket skt, Action closeCB) {
            try {
                this.skt = skt;
                this.closeCB = closeCB;
                //连接成功时调用（发送一条连接成功的信息）
                OnConnected();

                PEPkg pack = new PEPkg();
                //开始接受数据，这次获取数据只是获取数据头，目的是为了计算出数据头指向的数据的长度
                skt.BeginReceive(
                    pack.headBuff,
                    0,
                    pack.headLen,
                    SocketFlags.None,
                    new AsyncCallback(RcvHeadData),
                    //传入的objcet为pack
                    pack);
            }
            catch (Exception e) {
                PETool.LogMsg("StartRcvData:" + e.Message, LogLevel.Error);
            }
        }

        private void RcvHeadData(IAsyncResult ar) {
            try {
                PEPkg pack = (PEPkg)ar.AsyncState;
                //返回的len为该次接受数据的总长度
                int len = skt.EndReceive(ar);
                if (len > 0)
                {
                    //headIndex此时为数据的包括数据头和数据身体的总长度
                    pack.headIndex += len;
                    //如果数据的总长度得到的值要比数据头默认的4个字节要短，则说明该数据连数据头都不完整，继续接受数据直到数据完整
                    if (pack.headIndex < pack.headLen) {
                        skt.BeginReceive(
                            pack.headBuff,
                            pack.headIndex,
                            pack.headLen - pack.headIndex,
                            SocketFlags.None,
                            new AsyncCallback(RcvHeadData),
                            pack);
                    }
                    else {
                        //初始化数据身体，并且再次开始接受，这次接受的目的是按照数据头指定的长度接受数据身体
                        pack.InitBodyBuff();
                        skt.BeginReceive(pack.bodyBuff,
                            0,
                            pack.bodyLen,
                            SocketFlags.None,
                            new AsyncCallback(RcvBodyData),
                            pack);
                    }
                }
                //当len<=0的情况，即远程主机强制关闭了一个现有的连接，这时候服务器会疯狂接收到空数据，这时候就应该关闭掉这段Socket连接
                else
                {
                    //发送关闭连接的信息
                    OnDisConnected();
                    //关闭连接
                    Clear();
                }
            }
            catch (Exception e)
            {
                PETool.LogMsg("RcvHeadError:" + e.Message, LogLevel.Error);
            }
        }
        /// <summary>
        /// 先接收到了数据头后，再调用接受数据身子，获得完整的数据
        /// </summary>
        /// <param name="ar"></param>
        private void RcvBodyData(IAsyncResult ar)
        {
            try
            {
                PEPkg pack = (PEPkg)ar.AsyncState;
                int len = skt.EndReceive(ar);
                if (len > 0)
                {
                    pack.bodyIndex += len;
                    //同样，如果接收到的数据长度比目标数据长度要短，则继续接受直到数据完整
                    if (pack.bodyIndex < pack.bodyLen)
                    {
                        skt.BeginReceive(pack.bodyBuff,
                            pack.bodyIndex,
                            pack.bodyLen - pack.bodyIndex,
                            SocketFlags.None,
                            new AsyncCallback(RcvBodyData),
                            pack);
                    }
                    else
                    {
                        //反序列化数据
                        T msg = PETool.DeSerialize<T>(pack.bodyBuff);
                        OnReciveMsg(msg);

                        //loop recive，重置pack的设置后，继续开始接受数据，直到关闭连接
                        pack.ResetData();
                        skt.BeginReceive(
                            pack.headBuff,
                            0,
                            pack.headLen,
                            SocketFlags.None,
                            new AsyncCallback(RcvHeadData),
                            pack);
                    }
                }
                //同样，防止空数据，要关闭连接
                else
                {
                    OnDisConnected();
                    Clear();
                }
            }
            catch (Exception e)
            {
                PETool.LogMsg("RcvBodyError:" + e.Message, LogLevel.Error);
            }
        }
        #endregion

        #region Send
        /// <summary>
        /// Send message data
        /// </summary>
        public void SendMsg(T msg)
        {
            //将数据序列化成相应的格式，然后发送给服务器端或客户端
            byte[] data = PETool.PackLenInfo(PETool.Serialize<T>(msg));
            SendMsg(data);
        }

        /// <summary>
        /// Send binary data,通过已经建立好的Socket完成数据的发送
        /// </summary>
        public void SendMsg(byte[] data)
        {
            NetworkStream ns = null;
            try {
                ns = new NetworkStream(skt);
                if (ns.CanWrite)
                {
                    ns.BeginWrite(
                        data,
                        0,
                        data.Length,
                        new AsyncCallback(SendCB),
                        ns);
                }
            }
            catch (Exception e) {
                PETool.LogMsg("SndMsgError:" + e.Message, LogLevel.Error);
            }
        }

        private void SendCB(IAsyncResult ar)
        {
            NetworkStream ns = (NetworkStream)ar.AsyncState;
            try {
                ns.EndWrite(ar);
                ns.Flush();
                ns.Close();
            }
            catch (Exception e) {
                PETool.LogMsg("SndMsgError:" + e.Message, LogLevel.Error);
            }
        }
        #endregion

        /// <summary>
        /// Release Resource,关闭Socket连接
        /// </summary>
        private void Clear()
        {
            if (closeCB != null) {
                closeCB();
            }
            //关闭Socket
            skt.Close();
        }

        /// <summary>
        /// Connect network
        /// </summary>
        protected virtual void OnConnected()
        {
            PETool.LogMsg("New Seesion Connected.", LogLevel.Info);
        }

        /// <summary>
        /// Receive network message
        /// </summary>
        protected virtual void OnReciveMsg(T msg) {
            PETool.LogMsg("Receive Network Message.", LogLevel.Info);
        }

        /// <summary>
        /// Disconnect network
        /// </summary>
        protected virtual void OnDisConnected() {
            PETool.LogMsg("Session Disconnected.", LogLevel.Info);
        }
    }
}