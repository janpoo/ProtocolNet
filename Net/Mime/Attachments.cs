using System;
using System.Collections;
using System.ComponentModel;

namespace LumiSoft.Net.Mime
{
	/// <summary>
	/// Attachments collection.
	/// </summary>
	public class Attachments : ArrayList
	{
		/// <summary>
		/// 
		/// </summary>
		public Attachments()
		{
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="attachment"></param>
		/// <returns></returns>
		public int Add(Attachment attachment)
		{	
			return base.Add(attachment);
		}

		/// <summary>
		/// 
		/// </summary>
		public new Attachment this[int nIndex] 
		{ 
			get{ return (Attachment)base[nIndex]; }

			set{ base[nIndex] = value; }
		}

	}
}
