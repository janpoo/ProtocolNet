using System;
using System.IO;
using System.Text;
using System.Collections;

namespace LumiSoft.Net
{
	/// <summary>
	/// Byte[] line parser.
	/// </summary>
	public class StreamLineReader
	{
		private Stream m_StrmSource = null;

		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="strmSource"></param>
		public StreamLineReader(Stream strmSource)
		{
			m_StrmSource = strmSource;
		}


		#region function ReadLine

		/// <summary>
		/// Reads byte[] line from stream.
		/// </summary>
		/// <returns>Return null if end of stream reached.</returns>
		public byte[] ReadLine()
		{
			MemoryStream strmLineBuf = new MemoryStream();
			byte      prevByte = 0;

			int currByteInt = m_StrmSource.ReadByte();
			while(currByteInt > -1){
				strmLineBuf.WriteByte((byte)currByteInt);

				// Line found
				if((prevByte == (byte)'\r' && (byte)currByteInt == (byte)'\n')){
					strmLineBuf.SetLength(strmLineBuf.Length - 2); // Remove <CRLF>

					return strmLineBuf.ToArray();
				}
				
				// Store byte
				prevByte = (byte)currByteInt;

				// Read next byte
				currByteInt = m_StrmSource.ReadByte();				
			}

			// Line isn't terminated with <CRLF> and has some bytes left, return them.
			if(strmLineBuf.Length > 0){
				return strmLineBuf.ToArray();
			}

			return null;

			//**** This code below is very slow 

		/*	ArrayList lineBuf  = new ArrayList();
			byte      prevByte = 0;

			int currByteInt = m_StrmSource.ReadByte();
			while(currByteInt > -1){
				lineBuf.Add((byte)currByteInt);

				// Line found
				if((prevByte == (byte)'\r' && (byte)currByteInt == (byte)'\n')){
					byte[] retVal = new byte[lineBuf.Count-2];    // Remove <CRLF> 
					lineBuf.CopyTo(0,retVal,0,lineBuf.Count-2);

					return retVal;
				}
				
				// Store byte
				prevByte = (byte)currByteInt;

				// Read next byte
				currByteInt = m_StrmSource.ReadByte();				
			}

			// Line isn't terminated with <CRLF> and has some chars left, return them.
			if(lineBuf.Count > 0){
				byte[] retVal = new byte[lineBuf.Count];  
				lineBuf.CopyTo(0,retVal,0,lineBuf.Count);

				return retVal;
			}

			return null;*/
		}

		#endregion

	}
}
