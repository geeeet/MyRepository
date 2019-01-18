/****************************************************
	文件：PEPkg.cs
	作者：Plane
	邮箱: 1785275942@qq.com
	日期：2018/10/30 11:20   	
	功能：网络消息包
*****************************************************/

using System;

namespace PENet {
    /// <summary>
    /// 网络消息包
    /// </summary>
    class PEPkg
    {
        public int headLen = 4;
        public byte[] headBuff = null;
        public int headIndex = 0;

        public int bodyLen = 0;
        public byte[] bodyBuff = null;
        public int bodyIndex = 0;

        public PEPkg()
        {
            headBuff = new byte[4];
        }
        /// <summary>
        /// 将headBuff数据头中存储的数据转化为Int32位数据，该数据表示这条完整的数据的长度为多少
        /// </summary>
        public void InitBodyBuff()
        {
            //headBuff里存的是数据body的长度
            bodyLen = BitConverter.ToInt32(headBuff, 0);
            bodyBuff = new byte[bodyLen];
        }

        public void ResetData() {
            headIndex = 0;
            bodyLen = 0;
            bodyBuff = null;
            bodyIndex = 0;
        }
    }
}