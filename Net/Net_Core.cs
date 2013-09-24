using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Text.RegularExpressions;

namespace LumiSoft.Net
{
	#region enum AuthType

	/// <summary>
	/// Authentication type.
	/// </summary>
	public enum AuthType
	{
		/// <summary>
		/// Plain username/password authentication.
		/// </summary>
		Plain = 0,

		/// <summary>
		/// APOP
		/// </summary>
		APOP  = 1,

		/// <summary>
		/// Not implemented.
		/// </summary>
		LOGIN = 2,	
	
		/// <summary>
		/// Cram-md5 authentication.
		/// </summary>
		CRAM_MD5 = 3,	
	}

	#endregion

	/// <summary>
	/// Provides net core utility methods.
	/// </summary>
	public class Core
	{

		#region method DoPeriodHandling

		/// <summary>
		/// Does period handling.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="add_Remove">If true add periods, else removes periods.</param>
		/// <returns></returns>
		public static MemoryStream DoPeriodHandling(byte[] data,bool add_Remove)
		{
			using(MemoryStream strm = new MemoryStream(data)){
				return DoPeriodHandling(strm,add_Remove);
			}
		}

		/// <summary>
		/// Does period handling.
		/// </summary>
		/// <param name="strm">Input stream.</param>
		/// <param name="add_Remove">If true add periods, else removes periods.</param>
		/// <returns></returns>
		public static MemoryStream DoPeriodHandling(Stream strm,bool add_Remove)
		{
			return DoPeriodHandling(strm,add_Remove,true);
		}

		/// <summary>
		/// Does period handling.
		/// </summary>
		/// <param name="strm">Input stream.</param>
		/// <param name="add_Remove">If true add periods, else removes periods.</param>
		/// <param name="setStrmPosTo0">If true sets stream position to 0.</param>
		/// <returns></returns>
		public static MemoryStream DoPeriodHandling(Stream strm,bool add_Remove,bool setStrmPosTo0)
		{			
			MemoryStream replyData = new MemoryStream();

			byte[] crlf = new byte[]{(byte)'\r',(byte)'\n'};

			if(setStrmPosTo0){
				strm.Position = 0;
			}

			StreamLineReader r = new StreamLineReader(strm);
			byte[] line = r.ReadLine();

			// Loop through all lines
			while(line != null){
				if(line.Length > 0){
					if(line[0] == (byte)'.'){
						/* Add period Rfc 2821 4.5.2
						   -  Before sending a line of mail text, the SMTP client checks the
						   first character of the line.  If it is a period, one additional
						   period is inserted at the beginning of the line.
						*/
						if(add_Remove){
							replyData.WriteByte((byte)'.');
							replyData.Write(line,0,line.Length);
						}
						/* Remove period Rfc 2821 4.5.2
						 If the first character is a period , the first characteris deleted.							
						*/
						else{
							replyData.Write(line,1,line.Length-1);
						}
					}
					else{
						replyData.Write(line,0,line.Length);
					}
				}					

				replyData.Write(crlf,0,crlf.Length);

				// Read next line
				line = r.ReadLine();
			}

			replyData.Position = 0;

			return replyData;
		}

		#endregion


		#region method ReadLine

		/// <summary>
		/// Reads line of data from Socket.
		/// </summary>
		/// <param name="socket"></param>
		/// <returns></returns>
		public static string ReadLine(Socket socket)
		{
			return ReadLine(socket,500,60000);
		}

		/// <summary>
		/// Reads line of data from Socket.
		/// </summary>
		/// <param name="socket"></param>
		/// <returns></returns>
		public static string ReadLine(BufferedSocket socket)
		{
			return ReadLine(socket,500,60000);
		}

		/// <summary>
		/// Reads line of data from Socket.
		/// </summary>
		/// <param name="socket"></param>
		/// <param name="maxLen"></param>
		/// <param name="idleTimeOut"></param>
		/// <returns></returns>
		public static string ReadLine(BufferedSocket socket,int maxLen,int idleTimeOut)
		{
			MemoryStream storeStream = null;
			ReadReplyCode code = ReadData(socket,out storeStream,maxLen,idleTimeOut,"\r\n","\r\n");	
			if(code != ReadReplyCode.Ok){
				throw new ReadException(code,code.ToString());
			}

			return System.Text.Encoding.Default.GetString(storeStream.ToArray()).Trim();
		}

		/// <summary>
		/// Reads line of data from Socket.
		/// </summary>
		/// <param name="socket"></param>
		/// <param name="maxLen"></param>
		/// <param name="idleTimeOut"></param>
		/// <returns></returns>
		public static string ReadLine(Socket socket,int maxLen,int idleTimeOut)
		{
			MemoryStream storeStream = null;
			ReadReplyCode code = ReadData(socket,out storeStream,null,maxLen,idleTimeOut,"\r\n","\r\n");	
			if(code != ReadReplyCode.Ok){
				throw new ReadException(code,code.ToString());
			}

			return System.Text.Encoding.Default.GetString(storeStream.ToArray()).Trim();
		}

		#endregion

		#region method SendLine
		
		/// <summary>
		/// Sends line to Socket.
		/// </summary>
		/// <param name="socket"></param>
		/// <param name="lineData"></param>
		public static void SendLine(Socket socket,string lineData)
		{
			byte[] byte_data = System.Text.Encoding.Default.GetBytes(lineData + "\r\n");
			int countSended = socket.Send(byte_data);
			if(countSended != byte_data.Length){
				throw new Exception("Send error, didn't send all bytes !");
			}
		}

		/// <summary>
		/// Sends line to Socket.
		/// </summary>
		/// <param name="socket"></param>
		/// <param name="lineData"></param>
		public static void SendLine(BufferedSocket socket,string lineData)
		{
			byte[] byte_data = System.Text.Encoding.Default.GetBytes(lineData + "\r\n");
			int countSended = socket.Send(byte_data);
			if(countSended != byte_data.Length){
				throw new Exception("Send error, didn't send all bytes !");
			}
		}

		#endregion

		
		#region method ReadData

		/// <summary>
		/// Reads specified count of data from Socket.
		/// </summary>
		/// <param name="socket"></param>
		/// <param name="count">Number of bytes to read.</param>
		/// <param name="storeStrm"></param>
		/// <param name="storeToStream">If true stores readed data to stream, otherwise just junks specified amount of data.</param>
		/// <param name="cmdIdleTimeOut"></param>
		/// <returns></returns>
		public static ReadReplyCode ReadData(BufferedSocket socket,long count,Stream storeStrm,bool storeToStream,int cmdIdleTimeOut)
		{
			ReadReplyCode replyCode = ReadReplyCode.Ok;

			try{
				socket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReceiveTimeout,cmdIdleTimeOut);

				long readedCount  = 0;
				while(readedCount < count){
					byte[] b = new byte[4000];
					// Ensure that we don't get more data than needed
					if((count - readedCount) < 4000){
						b = new byte[count - readedCount];
					}

					int countRecieved = socket.Receive(b);
					if(countRecieved > 0){
						readedCount += countRecieved;

						if(storeToStream){
							storeStrm.Write(b,0,countRecieved);
						}
					}
					// Client disconnected
					else{						
						throw new Exception("Client disconnected");
					}
				}
			}
			catch(Exception x){
				replyCode = ReadReplyCode.UnKnownError;	

				if(x is SocketException){
					SocketException xS = (SocketException)x;
					if(xS.ErrorCode == 10060){
						replyCode = ReadReplyCode.TimeOut;
					}					
				}
			}

			return replyCode;
		}
/*
		/// <summary>
		/// Reads specified count of data from Socket.
		/// </summary>
		/// <param name="socket"></param>
		/// <param name="count">Number of bytes to read.</param>
		/// <param name="storeStrm"></param>
		/// <param name="storeToStream">If true stores readed data to stream, otherwise just junks data.</param>
		/// <param name="cmdIdleTimeOut"></param>
		/// <returns></returns>
		public static ReadReplyCode ReadData(Socket socket,long count,Stream storeStrm,bool storeToStream,int cmdIdleTimeOut)
		{
			ReadReplyCode replyCode = ReadReplyCode.Ok;

			try{
				socket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReceiveTimeout,cmdIdleTimeOut);

				long readedCount  = 0;
				while(readedCount < count){
					byte[] b = new byte[4000];
					// Ensure that we don't get more data than needed !!!
					if((count - readedCount) < 4000){
						b = new byte[count - readedCount];
					}

					int countRecieved = socket.Receive(b);
					if(countRecieved > 0){
						readedCount += countRecieved;

						if(storeToStream){
							storeStrm.Write(b,0,countRecieved);
						}
					}
					// Client disconnected
					else{						
						throw new Exception("Client disconnected");
					}
				}
			}
			catch(Exception x){Console.WriteLine(x.Message);
				replyCode = ReadReplyCode.UnKnownError;
			}

			return replyCode;
		}
*/
		#endregion

		#region method ReadData

		/// <summary>
		/// Reads reply from socket.
		/// </summary>
		/// <param name="socket"></param>
		/// <param name="replyData">Data that has been readen from socket.</param>
		/// <param name="maxLength">Maximum Length of data which may read.</param>
		/// <param name="cmdIdleTimeOut">Command idle time out in milliseconds.</param>
		/// <param name="terminator">Terminator string which terminates reading. eg '\r\n'.</param>
		/// <param name="removeFromEnd">Removes following string from reply.NOTE: removes only if ReadReplyCode is Ok.</param>		
		/// <returns>Return reply code.</returns>
		public static ReadReplyCode ReadData(BufferedSocket socket,out MemoryStream replyData,int maxLength,int cmdIdleTimeOut,string terminator,string removeFromEnd)
		{
			ReadReplyCode replyCode = ReadReplyCode.Ok;	
			replyData = null;

			try{
				socket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReceiveTimeout,cmdIdleTimeOut);

				replyData = new MemoryStream();
				_FixedStack stack = new _FixedStack(terminator);
				int nextReadWriteLen = 1;

				while(nextReadWriteLen > 0){
					//Read byte(s)
					byte[] b = new byte[nextReadWriteLen];
					int countRecieved = socket.Receive(b);
					if(countRecieved > 0){
						// Write byte(s) to buffer, if length isn't exceeded.
						if(replyCode != ReadReplyCode.LengthExceeded){							
							replyData.Write(b,0,countRecieved);
						}

						// Write to stack(terminator checker)
						nextReadWriteLen = stack.Push(b,countRecieved);

						//---- Check if maximum length is exceeded ---------------------------------//
						if(replyCode != ReadReplyCode.LengthExceeded && replyData.Length > maxLength){
							replyCode = ReadReplyCode.LengthExceeded;
						}
						//--------------------------------------------------------------------------//
					}
					// Client disconnected
					else{
						throw new Exception("Client disconnected");
					}
				}

				// If reply is ok then remove chars if any specified by 'removeFromEnd'.
				if(replyCode == ReadReplyCode.Ok && removeFromEnd.Length > 0){					
					replyData.SetLength(replyData.Length - removeFromEnd.Length);				
				}
			}
			catch(Exception x){
				replyCode = ReadReplyCode.UnKnownError;	

				if(x is SocketException){
					SocketException xS = (SocketException)x;
					if(xS.ErrorCode == 10060){
						replyCode = ReadReplyCode.TimeOut;
					}					
				}
			}

			return replyCode;
		}

		/// <summary>
		/// Reads reply from socket.
		/// </summary>
		/// <param name="socket"></param>
		/// <param name="replyData">Data that has been readen from socket.</param>
		/// <param name="addData">Data that has will be written at the beginning of read data. This param may be null.</param>
		/// <param name="maxLength">Maximum Length of data which may read.</param>
		/// <param name="cmdIdleTimeOut">Command idle time out in milliseconds.</param>
		/// <param name="terminator">Terminator string which terminates reading. eg '\r\n'.</param>
		/// <param name="removeFromEnd">Removes following string from reply.NOTE: removes only if ReadReplyCode is Ok.</param>		
		/// <returns>Return reply code.</returns>
		public static ReadReplyCode ReadData(Socket socket,out MemoryStream replyData,byte[] addData,int maxLength,int cmdIdleTimeOut,string terminator,string removeFromEnd)
		{	
			ReadReplyCode replyCode = ReadReplyCode.Ok;	
			replyData = null;

			try{
				socket.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReceiveTimeout,cmdIdleTimeOut);

				replyData = new MemoryStream();
				_FixedStack stack = new _FixedStack(terminator);
				int nextReadWriteLen = 1;

				while(nextReadWriteLen > 0){
					//Read byte(s)
					byte[] b = new byte[nextReadWriteLen];
					int countRecieved = socket.Receive(b);
					if(countRecieved > 0){
						// Write byte(s) to buffer, if length isn't exceeded.
						if(replyCode != ReadReplyCode.LengthExceeded){							
							replyData.Write(b,0,countRecieved);
						}

						// Write to stack(terminator checker)
						nextReadWriteLen = stack.Push(b,countRecieved);

						//---- Check if maximum length is exceeded ---------------------------------//
						if(replyCode != ReadReplyCode.LengthExceeded && replyData.Length > maxLength){
							replyCode = ReadReplyCode.LengthExceeded;
						}
						//--------------------------------------------------------------------------//
					}
					// Client disconnected
					else{
						throw new Exception("Client disconnected");
					}
				}

				// If reply is ok then remove chars if any specified by 'removeFromEnd'.
				if(replyCode == ReadReplyCode.Ok && removeFromEnd.Length > 0){					
					replyData.SetLength(replyData.Length - removeFromEnd.Length);				
				}
			}
			catch(Exception x){
				replyCode = ReadReplyCode.UnKnownError;	

				if(x is SocketException){
					SocketException xS = (SocketException)x;
					if(xS.ErrorCode == 10060){
						replyCode = ReadReplyCode.TimeOut;
					}					
				}
			}

			return replyCode;
		}

		#endregion

		
		#region method GetHostName

		/// <summary>
		/// Gets host name. If fails returns 'UnkownHost'.
		/// </summary>
		/// <param name="IP"></param>
		/// <returns></returns>
		public static string GetHostName(IPAddress IP)
		{
			try{
				return System.Net.Dns.GetHostByAddress(IP).HostName;
			}
			catch{
				return "UnkownHost";
			}
		}

		#endregion


		#region method GetArgsText

		/// <summary>
		/// Gets argument part of command text.
		/// </summary>
		/// <param name="input">Input srting from where to remove value.</param>
		/// <param name="cmdTxtToRemove">Command text which to remove.</param>
		/// <returns></returns>
		public static string GetArgsText(string input,string cmdTxtToRemove)
		{
			string buff = input.Trim();
			if(buff.Length >= cmdTxtToRemove.Length){
				buff = buff.Substring(cmdTxtToRemove.Length);
			}
			buff = buff.Trim();

			return buff;
		}

		#endregion

		
		#region method IsNumber

		/// <summary>
		/// Checks if specified string is number(long).
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static bool IsNumber(string str)
		{
			try{
				Convert.ToInt64(str);
				return true;
			}
			catch{
				return false;
			}
		}

		#endregion


		#region method QuotedPrintableDecode

		/// <summary>
		/// quoted-printable decoder.
		/// </summary>
		/// <param name="encoding">Input string encoding.</param>
		/// <param name="data">Data which to encode.</param>
		/// <returns>Returns decoded data with specified encoding.</returns>
		public static string QuotedPrintableDecode(System.Text.Encoding encoding,byte[] data)
		{
			MemoryStream strm = new MemoryStream(data);			
			MemoryStream dStrm = new MemoryStream();

			int b = strm.ReadByte();
			while(b > -1){
				// Hex eg. =E4
				if(b == '='){
					byte[] buf = new byte[2];
					strm.Read(buf,0,2);

					// <CRLF> followed by =, it's splitted line
					if(!(buf[0] == '\r' && buf[1] == '\n')){
						try{
							int val = int.Parse(System.Text.Encoding.ASCII.GetString(buf),System.Globalization.NumberStyles.HexNumber);
							dStrm.WriteByte((byte)val);
						}
						catch{ // If worng hex value, just skip this chars							
						}
					}
				}
				else{
					dStrm.WriteByte((byte)b);
				}

				b = strm.ReadByte();
			}

			return encoding.GetString(dStrm.ToArray());

	/*		MemoryStream strm = new MemoryStream(data);
			int b = strm.ReadByte();

			MemoryStream dStrm = new MemoryStream();

			while(b > -1){
				// Hex eg. =E4
				if(b == '='){
					byte[] buf = new byte[2];
					strm.Read(buf,0,2);

					// <CRLF> followed by =, it's splitted line
					if(!(buf[0] == '\r' && buf[1] == '\n')){
						try{
							int val = int.Parse(System.Text.Encoding.ASCII.GetString(buf),System.Globalization.NumberStyles.HexNumber);
							string encodedChar = encoding.GetString(new byte[]{(byte)val});
							byte[] d = System.Text.Encoding.Unicode.GetBytes(encodedChar);
							dStrm.Write(d,0,d.Length);

							System.Windows.Forms.MessageBox.Show( System.Text.Encoding.Unicode.GetString(new byte[]{(byte)val}));
						}
						catch{ // If worng hex value, just skip this chars							
						}
					}
				}
				else{
					string encodedChar = encoding.GetString(new byte[]{(byte)b});
					byte[] d = System.Text.Encoding.Unicode.GetBytes(encodedChar);
					dStrm.Write(d,0,d.Length);
				}

				b = strm.ReadByte();
			}

			return System.Text.Encoding.Unicode.GetString(dStrm.ToArray());*/
		}

		#endregion

		#region method QDecode

		/// <summary>
		/// "Q" decoder. This is same as quoted-printable, except '_' is converted to ' '.
		/// </summary>
		/// <param name="encoding">Input string encoding.</param>
		/// <param name="data">String which to encode.</param>
		/// <returns>Returns decoded string.</returns>		
		public static string QDecode(System.Text.Encoding encoding,string data)
		{
			return QuotedPrintableDecode(encoding,System.Text.Encoding.ASCII.GetBytes(data)).Replace("_"," ");
		}

		#endregion

		#region method CanonicalDecode

		/// <summary>
		/// Canonical decoding. Decodes all canonical encoding occurences in specified text.
		/// Usually mime message header unicode/8bit values are encoded as Canonical.
		/// Format: =?charSet?type[Q or B]?encoded string?= .
		/// </summary>
		/// <param name="text">Text to decode.</param>
		/// <returns>Returns decoded text.</returns>
		public static string CanonicalDecode(string text)
		{
			// =?charSet?type[Q or B]?encoded string?=

			Regex regex = new Regex(@"\=\?(?<charSet>[\w\-]*)\?(?<type>[qQbB])\?(?<text>[\w\W]*)\?\=");

			MatchCollection m = regex.Matches(text);
			foreach(Match match in m){
				try{
					System.Text.Encoding enc = System.Text.Encoding.GetEncoding(match.Groups["charSet"].Value);
					// QDecode
					if(match.Groups["type"].Value.ToLower() == "q"){
						text = text.Replace(match.Value,Core.QDecode(enc,match.Groups["text"].Value));
					}
					// Base64
					else{
						text = text.Replace(match.Value,enc.GetString(Convert.FromBase64String(match.Groups["text"].Value)));
					}
				}
				catch{
					// If parsing fails, just leave this string as is
				}
			}

			return text;
		}

		#endregion

		#region method CanonicalEncode

		/// <summary>
		/// Canonical encoding.
		/// </summary>
		/// <param name="str">String to encode.</param>
		/// <param name="charSet">To which charset to encode string.</param>
		/// <returns>Returns encoded text.</returns>
		public static string CanonicalEncode(string str,string charSet)
		{
			// Contains non ascii chars, need to encode
			if(!IsAscii(str)){
				string retVal = "=?" + charSet + "?" + "B?";
				retVal += Convert.ToBase64String(System.Text.Encoding.GetEncoding(charSet).GetBytes(str));
				retVal += "?=";

				return retVal;
			}

			return str;
		}

		#endregion

		#region method IsAscii

		/// <summary>
		/// Checks if specified string data is acii data.
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static bool IsAscii(string data)
		{			
			foreach(char c in data){
				if((int)c > 127){ 
					return false;
				}
			}

			return true;
		}

		#endregion

	}
}
