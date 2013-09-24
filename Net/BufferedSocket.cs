using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace LumiSoft.Net
{
	/// <summary>
	/// Sokcet + buffer. Socket data reads are buffered. At first Recieve returns data from
	/// internal buffer and if no data available, gets more from socket. Socket buffer is also
	/// user settable, you can add data to socket buffer directly with AppendBuffer().
	/// </summary>
	public class BufferedSocket
	{
		private Socket m_pSocket = null;
		private byte[] m_Buffer  = null;
		private long   m_BufPos  = 0;
		private bool   m_Closed  = false;

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="socket">Source socket which to buffer.</param>
		public BufferedSocket(Socket socket)
		{
			m_pSocket = socket;
			m_Buffer  = new byte[0];
		}

		#region method BeginConnect

		/// <summary>
		/// 
		/// </summary>
		/// <param name="remoteEP"></param>
		/// <param name="callback"></param>
		/// <param name="state"></param>
		/// <returns></returns>
		public IAsyncResult BeginConnect(EndPoint remoteEP,AsyncCallback callback,object state)
		{
			return m_pSocket.BeginConnect(remoteEP,callback,state);
		}

		#endregion

		#region method EndConnect

		/// <summary>
		/// 
		/// </summary>
		/// <param name="asyncResult"></param>
		/// <returns></returns>
		public void EndConnect(IAsyncResult asyncResult)
		{
			m_pSocket.EndConnect(asyncResult);
		}

		#endregion


		#region method Receive

		/// <summary>
		/// Receives data from buffer. If there isn't data in buffer, then receives more data from socket.
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public int Receive(byte[] buffer)
		{
			// There isn't data in buffer, get more
			if(this.AvailableInBuffer == 0){
				byte[] buf = new byte[10000];
				int countReaded = m_pSocket.Receive(buf);
				
				if(countReaded != buf.Length){
					m_Buffer = new byte[countReaded];
					Array.Copy(buf,0,m_Buffer,0,countReaded);
				}
				else{
					m_Buffer = buf;
				}

				m_BufPos = 0;
			}

			return ReceiveFromFuffer(buffer);
/*
			int countInBuff = this.AvailableInBuffer;
			// There is more data in buffer as requested
			if(countInBuff > buffer.Length){
				Array.Copy(m_Buffer,m_BufPos,buffer,0,buffer.Length);

				m_BufPos += buffer.Length;

				return buffer.Length;
			}
			else{
				Array.Copy(m_Buffer,m_BufPos,buffer,0,countInBuff);

				// Reset buffer and pos, because we used all data from buffer
				m_Buffer = new byte[0];
				m_BufPos = 0;

				return countInBuff;
			}*/
		}

		#endregion

		#region method BeginReceive

		/// <summary>
		/// 
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="size"></param>
		/// <param name="socketFlags"></param>
		/// <param name="callback"></param>
		/// <param name="state"></param>
		public void BeginReceive(byte[] buffer,int offset,int size,SocketFlags socketFlags,AsyncCallback callback,object state)
		{
			m_pSocket.BeginReceive(buffer,offset,size,socketFlags,callback,state);
		}

		#endregion

		#region method EndReceive

		/// <summary>
		/// 
		/// </summary>
		/// <param name="asyncResult"></param>
		/// <returns></returns>
		public int EndReceive(IAsyncResult asyncResult)
		{
			return m_pSocket.EndReceive(asyncResult);
		}

		#endregion

		#region method Send

		/// <summary>
		/// 
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public int Send(byte[] buffer)
		{
			return m_pSocket.Send(buffer);
		}

		#endregion


		#region method SetSocketOption

		/// <summary>
		/// 
		/// </summary>
		/// <param name="otpionLevel"></param>
		/// <param name="optionName"></param>
		/// <param name="optionValue"></param>
		public void SetSocketOption(SocketOptionLevel otpionLevel,SocketOptionName optionName,int optionValue)
		{
			m_pSocket.SetSocketOption(otpionLevel,optionName,optionValue);
		}

		#endregion


		#region method Shutdown

		/// <summary>
		/// 
		/// </summary>
		/// <param name="how"></param>
		public void Shutdown(SocketShutdown how)
		{
			m_Closed = true;
			m_pSocket.Shutdown(how);
		}

		#endregion

		#region method Close

		/// <summary>
		/// 
		/// </summary>
		public void Close()
		{
			m_Closed = true;
			m_pSocket.Close();
			m_pSocket = null;
			m_Buffer = new byte[0];
		}

		#endregion

		
		#region method ReceiveFromFuffer

		/// <summary>
		/// Receives data from buffer.
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public int ReceiveFromFuffer(byte[] buffer)
		{
			int countInBuff = this.AvailableInBuffer;
			// There is more data in buffer as requested
			if(countInBuff > buffer.Length){
				Array.Copy(m_Buffer,m_BufPos,buffer,0,buffer.Length);

				m_BufPos += buffer.Length;

				return buffer.Length;
			}
			else{
				Array.Copy(m_Buffer,m_BufPos,buffer,0,countInBuff);

				// Reset buffer and pos, because we used all data from buffer
				m_Buffer = new byte[0];
				m_BufPos = 0;

				return countInBuff;
			}
		}

		#endregion
		
		#region method AppendBuffer

		internal void AppendBuffer(byte[] data,int length)
		{
			if(m_Buffer.Length == 0){
				m_Buffer = new byte[length];
				Array.Copy(data,0,m_Buffer,0,length);
			}
			else{
				byte[] newBuff = new byte[m_Buffer.Length + length];
				Array.Copy(m_Buffer,0,newBuff,0,m_Buffer.Length);
				Array.Copy(data,0,newBuff,m_Buffer.Length,length);

				m_Buffer = newBuff;
			}
		}

		#endregion


		#region Properties Implementation

		internal Socket Socket
		{
			get{ return m_pSocket; }
		}

		internal byte[] Buffer
		{
			get{ return m_Buffer; }
		}

		/// <summary>
		/// 
		/// </summary>
		public int Available
		{
			get{ return (m_Buffer.Length - (int)m_BufPos) + m_pSocket.Available; }
		}

		/// <summary>
		/// 
		/// </summary>
		public bool Connected
		{
			get{ return m_pSocket.Connected; }
		}

		/// <summary>
		/// 
		/// </summary>
		public bool IsClosed
		{
			get{ return m_Closed; }
		}

		/// <summary>
		/// 
		/// </summary>
		public EndPoint LocalEndPoint
		{
			get{ return m_pSocket.LocalEndPoint; }
		}

		/// <summary>
		/// 
		/// </summary>
		public EndPoint RemoteEndPoint
		{
			get{ return m_pSocket.RemoteEndPoint; }
		}


		/// <summary>
		/// Gets the amount of data in buffer.
		/// </summary>
		public int AvailableInBuffer
		{
			get{ return m_Buffer.Length - (int)m_BufPos; }
		}

		#endregion
	}
}
