using System;
using System.IO;
using System.Collections;
using LumiSoft.Net.Mime;

namespace LumiSoft.Net.IMAP.Server
{
	/// <summary>
	/// FETCH command helper methods.
	/// </summary>
	internal class FetchHelper
	{				
		#region function ParseHeaderFields

		/// <summary>
		/// Returns requested header fields lines.
		/// </summary>
		/// <param name="fieldsStr">Header fields to get.</param>
		/// <param name="data">Message data.</param>
		/// <returns></returns>
		public static string ParseHeaderFields(string fieldsStr,byte[] data)
		{
			string retVal = "";

			string[] fields = fieldsStr.Split(' ');
            using(MemoryStream mStrm = new MemoryStream(data)){
				TextReader r = new StreamReader(mStrm);
				string line = r.ReadLine();
				
				bool fieldFound = false;
				// Loop all header lines
				while(line != null){ 
					// End of header
					if(line.Length == 0){
						break;
					}

					// Field continues
					if(fieldFound && line.StartsWith("\t")){
						retVal += line + "\r\n";
					}
					else{
						fieldFound = false;

						// Check if wanted field
						foreach(string field in fields){
							if(line.Trim().ToLower().StartsWith(field.Trim().ToLower())){
								retVal += line + "\r\n";
								fieldFound = true;
							}
						}
					}

					line = r.ReadLine();
				}
			}

			return retVal;
		}

		#endregion

		#region function ParseHeaderFieldsNot

		/// <summary>
		/// Returns header fields lines except requested.
		/// </summary>
		/// <param name="fieldsStr">Header fields to skip.</param>
		/// <param name="data">Message data.</param>
		/// <returns></returns>
		public static string ParseHeaderFieldsNot(string fieldsStr,byte[] data)
		{
			string retVal = "";

			string[] fields = fieldsStr.Split(' ');
            using(MemoryStream mStrm = new MemoryStream(data)){
				TextReader r = new StreamReader(mStrm);
				string line = r.ReadLine();
				
				bool fieldFound = false;
				// Loop all header lines
				while(line != null){ 
					// End of header
					if(line.Length == 0){
						break;
					}

					// Filed continues
					if(fieldFound && line.StartsWith("\t")){
						retVal += line + "\r\n";
					}
					else{
						fieldFound = false;

						// Check if wanted field
						foreach(string field in fields){
							if(line.Trim().ToLower().StartsWith(field.Trim().ToLower())){								
								fieldFound = true;
							}
						}

						if(!fieldFound){
							retVal += line + "\r\n";
						}
					}

					line = r.ReadLine();
				}
			}

			return retVal;
		}

		#endregion

		#region function ParseMimeEntry

		/// <summary>
		/// Returns requested mime entry data.
		/// </summary>
		/// <param name="parser"></param>
		/// <param name="mimeEntryNo"></param>
		/// <returns>Returns requested mime entry data or NULL if requested entri doesn't exist.</returns>
		public static byte[] ParseMimeEntry(MimeParser parser,int mimeEntryNo)
		{
			if(mimeEntryNo > 0 && mimeEntryNo <= parser.MimeEntries.Count){
				return ((MimeEntry)parser.MimeEntries[mimeEntryNo - 1]).Data;
			}

			return null;
		}

		#endregion


		#region construct ConstructEnvelope

		/// <summary>
		/// Construct FETCH ENVELOPE response.
		/// </summary>
		/// <param name="parser"></param>
		/// <returns></returns>
		public static string ConstructEnvelope(MimeParser parser)
		{
			/* Rfc 3501 7.4.2
				ENVELOPE
				A parenthesized list that describes the envelope structure of a
				message.  This is computed by the server by parsing the
				[RFC-2822] header into the component parts, defaulting various
				fields as necessary.

				The fields of the envelope structure are in the following
				order: date, subject, from, sender, reply-to, to, cc, bcc,
				in-reply-to, and message-id.  The date, subject, in-reply-to,
				and message-id fields are strings.  The from, sender, reply-to,
				to, cc, and bcc fields are parenthesized lists of address
				structures.

				An address structure is a parenthesized list that describes an
				electronic mail address.  The fields of an address structure
				are in the following order: personal name, [SMTP]
				at-domain-list (source route), mailbox name, and host name.

				[RFC-2822] group syntax is indicated by a special form of
				address structure in which the host name field is NIL.  If the
				mailbox name field is also NIL, this is an end of group marker
				(semi-colon in RFC 822 syntax).  If the mailbox name field is
				non-NIL, this is a start of group marker, and the mailbox name
				field holds the group name phrase.

				If the Date, Subject, In-Reply-To, and Message-ID header lines
				are absent in the [RFC-2822] header, the corresponding member
				of the envelope is NIL; if these header lines are present but
				empty the corresponding member of the envelope is the empty
				string.
			*/
			// ((sender))
			// ENVELOPE ("date" "subject" from sender reply-to to cc bcc in-reply-to "messageID")
			
			string envelope = "ENVELOPE (";
			
			// date
			envelope += "\"" + parser.MessageDate.ToString("r",System.Globalization.DateTimeFormatInfo.InvariantInfo) + "\" ";
			
			// subject
			envelope += "\"" + parser.Subject + "\" ";

			// from
			// ToDo: May be multiple senders
			LumiSoft.Net.Mime.Parser.eAddress adr = new LumiSoft.Net.Mime.Parser.eAddress(parser.From);
			envelope += "((\"" + adr.Name + "\" NIL \"" + adr.Mailbox + "\" \"" + adr.Domain + "\")) ";

			// sender
			// ToDo: May be multiple senders
			envelope += "((\"" + adr.Name + "\" NIL \"" + adr.Mailbox + "\" \"" + adr.Domain + "\")) ";

			// reply-to
			string replyTo = MimeParser.ParseHeaderField("reply-to:",parser.Headers);
			if(replyTo.Length > 0){
				envelope += "(";
				foreach(string recipient in replyTo.Split(';')){
					LumiSoft.Net.Mime.Parser.eAddress adrTo = new LumiSoft.Net.Mime.Parser.eAddress(recipient);
					envelope += "(\"" + adrTo.Name + "\" NIL \"" + adrTo.Mailbox + "\" \"" + adrTo.Domain + "\") ";
				}
				envelope = envelope.TrimEnd();
				envelope += ") ";
			}
			else{
				envelope += "NIL ";				
			}

			// to
			string[] to = parser.To;
			envelope += "(";
			foreach(string recipient in to){
				LumiSoft.Net.Mime.Parser.eAddress adrTo = new LumiSoft.Net.Mime.Parser.eAddress(recipient);
				envelope += "(\"" + adrTo.Name + "\" NIL \"" + adrTo.Mailbox + "\" \"" + adrTo.Domain + "\") ";
			}
			envelope = envelope.TrimEnd();
			envelope += ") ";

			// cc
			string cc = MimeParser.ParseHeaderField("CC:",parser.Headers);
			if(cc.Length > 0){
				envelope += "(";
				foreach(string recipient in cc.Split(';')){
					LumiSoft.Net.Mime.Parser.eAddress adrTo = new LumiSoft.Net.Mime.Parser.eAddress(recipient);
					envelope += "(\"" + adrTo.Name + "\" NIL \"" + adrTo.Mailbox + "\" \"" + adrTo.Domain + "\") ";
				}
				envelope = envelope.TrimEnd();
				envelope += ") ";
			}
			else{
				envelope += "NIL ";				
			}

			// bcc
			string bcc = MimeParser.ParseHeaderField("BCC:",parser.Headers);
			if(bcc.Length > 0){
				envelope += "(";
				foreach(string recipient in bcc.Split(';')){
					LumiSoft.Net.Mime.Parser.eAddress adrTo = new LumiSoft.Net.Mime.Parser.eAddress(recipient);
					envelope += "(\"" + adrTo.Name + "\" NIL \"" + adrTo.Mailbox + "\" \"" + adrTo.Domain + "\") ";
				}
				envelope = envelope.TrimEnd();
				envelope += ") ";
			}
			else{
				envelope += "NIL ";				
			}

			// in-reply-to
			string inReplyTo = MimeParser.ParseHeaderField("in-reply-to:",parser.Headers);
			if(inReplyTo.Length > 0){
				envelope += "\"" + inReplyTo + "\"";
			}
			else{
				envelope += "NIL ";
			}

			// message-id
			if(parser.MessageID.Length > 0){
				envelope += "\"" + parser.MessageID + "\"";
			}
			else{
				envelope += "NIL";
			}

			envelope += ")";

			return envelope;
		}

		#endregion

		#region construct BODYSTRUCTURE

		/// <summary>
		/// Constructs FETCH BODY and BODYSTRUCTURE response.
		/// </summary>
		/// <param name="parser"></param>
		/// <param name="bodystructure"></param>
		/// <returns></returns>
		public static string ConstructBodyStructure(MimeParser parser,bool bodystructure)
		{
			/* Rfc 3501 7.4.2 BODYSTRUCTURE

				For example, a simple text message of 48 lines and 2279 octets
				can have a body structure of: ("TEXT" "PLAIN" ("CHARSET"
				"US-ASCII") NIL NIL "7BIT" 2279 48)
				
				For example, a two part message consisting of a text and a
				BASE64-encoded text attachment can have a body structure of:
				(("TEXT" "PLAIN" ("CHARSET" "US-ASCII") NIL NIL "7BIT" 1152
				23)("TEXT" "PLAIN" ("CHARSET" "US-ASCII" "NAME" "cc.diff")
				"<960723163407.20117h@cac.washington.edu>" "Compiler diff"
				"BASE64" 4554 73) "MIXED")


				// Basic fields for multipart
				(nestedMimeEntries) conentSubType
			
				// Extention data for multipart
				(conentTypeSubFields) contentDisposition contentLanguage [contentLocation]
				
				contentDisposition  - ("disposition" {(subFileds) or NIL}) or NIL
							
				contentType         - 'TEXT'
				conentSubType       - 'PLAIN'
				conentTypeSubFields - '("CHARSET" "iso-8859-1" ...)'
				contentID           - Content-ID field
				contentDescription  - Content-Description field
				contentEncoding     - 'quoted-printable'
				contentSize         - mimeEntry NOT ENCODED data size
				[envelope]          - NOTE: included only if contentType = "message" !!!
				[contentLines]      - number of content lines. NOTE: included only if contentType = "text" !!!
				
				// Basic fields for non-multipart
				contentType conentSubType (conentTypeSubFields) contentID contentDescription contentEncoding contentSize contentLines

				// Extention data for non-multipart
				contentDataMd5 contentDisposition contentLanguage [conentLocation]
			
			
				body language
					A string or parenthesized list giving the body language
					value as defined in [LANGUAGE-TAGS].

				body location
					A string list giving the body content URI as defined in	[LOCATION].
				
			*/
						
			string str = "";

			if(bodystructure){
				str += "BODYSTRUCTURE ";
			}
			else{
				str += "BODY ";
			}
            
			if(parser.ContentType.ToLower().IndexOf("multipart") > -1){
				str += "(";
			}

			str += ConstructPart(parser.MimeEntries,bodystructure);
			
			if(parser.ContentType.ToLower().IndexOf("multipart") > -1){
				// conentSubType
				if(parser.ContentType.Split('/').Length == 2){
					str += " \"" + parser.ContentType.Split('/')[1].Replace(";","") + "\"";					
				}
				else{
					str += " NIL";
				}

				// Need to add extended fields
				if(bodystructure){
					str += " ";

					// conentTypeSubFields
					string longContentType = MimeParser.ParseHeaderField("Content-Type:",parser.Headers);
					if(longContentType.IndexOf(";") > -1){
						str += "(";
						string[] fields = longContentType.Split(';');
						for(int i=1;i<fields.Length;i++){
							string[] nameValue = fields[i].Replace("\"","").Trim().Split(new char[]{'='},2);

							str += "\"" + nameValue[0] + "\" \"" + nameValue[1] + "\"";

							if(i < fields.Length - 1){
								str += " ";
							}
						}
						str += ") ";
					}

					// contentDisposition
					str += "NIL ";

					// contentLanguage
					str += "NIL";
				}
				
				str += ")";
			}	

			return str;
		}

		private static string ConstructPart(ArrayList entries,bool bodystructure)
		{
			string str = "";

			foreach(MimeEntry ent in entries){
				// multipart
				if(ent.MimeEntries != null){
					str += ConstructMultiPart(ent,bodystructure);
				}
				// non-multipart
				else{
					str +=  ConstructNonMultiPart(ent,bodystructure);
				}
			}

			return str;
		}

		private static string ConstructMultiPart(MimeEntry ent,bool bodystructure)
		{
			string str = "(";

			str += ConstructPart(ent.MimeEntries,bodystructure);

			str += " ";
			
			// conentSubType
			if(ent.ContentType.Split('/').Length == 2){
				str += "\"" + ent.ContentType.Split('/')[1].Replace(";","") + "\""; 
			}
			else{
				str += "NIL";
			}

			// Need to add extended fields
			if(bodystructure){
				str += " ";

				// conentTypeSubFields
				string longContentType = MimeParser.ParseHeaderField("Content-Type:",ent.Headers);
				if(longContentType.IndexOf(";") > -1){
					str += "(";
					string[] fields = longContentType.Split(';');
					for(int i=1;i<fields.Length;i++){
						string[] nameValue = fields[i].Replace("\"","").Trim().Split(new char[]{'='},2);

						str += "\"" + nameValue[0] + "\" \"" + nameValue[1] + "\"";

						if(i < fields.Length - 1){
							str += " ";
						}
					}
					str += ") ";
				}

				// contentDisposition
				str += "NIL ";

				// contentLanguage
				str += "NIL";
			}

			str += ")";

			return str;			
		}

		private static string ConstructNonMultiPart(MimeEntry ent,bool bodystructure)
		{			
			string str =  "(";

			// contentType
			str += "\"" + ent.ContentType.Split('/')[0] + "\" ";

			// conentSubType
			if(ent.ContentType.Split('/').Length == 2){
				str += "\"" + ent.ContentType.Split('/')[1].Replace(";","") + "\" "; 
			}
			else{
				str += "NIL ";
			}

			// conentTypeSubFields
			string longContentType = MimeParser.ParseHeaderField("Content-Type:",ent.Headers);
			if(longContentType.IndexOf(";") > -1){
				str += "(";
				string[] fields = longContentType.Split(';');
				for(int i=1;i<fields.Length;i++){
					string[] nameValue = fields[i].Replace("\"","").Trim().Split(new char[]{'='},2);

					str += "\"" + nameValue[0] + "\" \"" + nameValue[1] + "\"";

					if(i < fields.Length - 1){
						str += " ";
					}
				}
				str += ") ";
			}
			else{
				// if content is attachment and content type name filed is missing, use filename for it
				string fileName = MimeParser.ParseHeaderFiledSubField("Content-Disposition:","filename",ent.Headers);
				if(fileName.Length > 0){
					str += "(\"name\" \"" + fileName + "\") ";
				}
				else{
					str += "NIL ";
				}
			}

			// contentID
			string contentID = MimeParser.ParseHeaderField("Content-ID:",ent.Headers);
			if(contentID.Length > 0){
				str += "\"" + contentID + "\" ";
			}
			else{
				str += "NIL ";
			}

			// contentDescription
			string contentDescription = MimeParser.ParseHeaderField("Content-Description:",ent.Headers);
			if(contentDescription.Length > 0){
				str += "\"" + contentDescription + "\" ";
			}
			else{
				str += "NIL ";
			}

			// contentEncoding
			str += "\"" + ent.ContentEncoding + "\" ";

			// contentSize
			str += ent.DataNonDecoded.Length + " ";

			// envelope NOTE: included only if contentType = "message" !!!

			// contentLines NOTE: included only if contentType = "text" !!!
			if(ent.ContentType.ToLower().IndexOf("text") > -1){
				StreamLineReader r = new StreamLineReader(new MemoryStream(ent.DataNonDecoded));
				int nLines = 0;
				byte[] line = new byte[0];
				while(line != null){
					line = r.ReadLine();
					nLines++;
				}
				str += nLines;
			}
		
			// Need to add extended fields
			if(bodystructure){
				str += " ";

				// md5
				str += "NIL ";

				// contentDisposition
				string contDispos = MimeParser.ParseHeaderField("Content-Disposition:",ent.Headers);
				if(contDispos.Length > 0){
					str += "(";

					string[] fields = contDispos.Split(';');

					str += "\"" + fields[0] + "\" ";

					if(fields.Length > 1){
						str += "(";
						for(int i=1;i<fields.Length;i++){
							string[] nameValue = fields[i].Replace("\"","").Trim().Split(new char[]{'='},2);

							str += "\"" + nameValue[0] + "\" \"" + nameValue[1] + "\"";

							if(i < fields.Length - 1){
								str += " ";
							}
						}
						str += ")";
					}
					else{
						str += "NIL";
					}

					str += ") ";
				}
				else{
					str += "NIL ";
				}

				// contentLanguage
				str += "NIL";
			}

			str += ")";

			return str;
		}

		#endregion

	}
}
