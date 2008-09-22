// This module features so called "unsafe" code that can be used to improve performance.
// However it appears that it usage of it actually slowed down parser, so this "unsafe" code is not
// active by default. 
//#define UNSAFE_CODE

using System;
using System.IO;
using System.Collections;
using System.Text;

/*

Copyright (c) Alex Chudnovsky, Majestic-12 Ltd (UK). 2005+ All rights reserved
Web:		http://www.majestic12.co.uk
E-mail:		alexc@majestic12.co.uk

Redistribution and use in source and binary forms, with or without modification, are 
permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright notice, this list of conditions 
		and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions 
		and the following disclaimer in the documentation and/or other materials provided with the distribution.
    * Neither the name of the Majestic-12 nor the names of its contributors may be used to endorse or 
		promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE 
USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/

/// <summary>
/// Description:	HTMLparser -- low level class that splits HTML into tokens 
///					such as tag, comments, text etc.
///					
///					Change source with great care - a lot of effort went into optimising it to make sure it performs
///					very well, for this reason some range checks were removed in cases when big enough buffers
///					were sufficient for 99.9999% of HTML pages out there.
///					
///					This does not mean you won't get an exception, so when you are parsing, then make sure you
///					catch exceptions from parser.
///					
/// Author:			Alex Chudnovsky <alexc@majestic12.co.uk>
/// 	
/// History:		
///					4/02/06 v1.0.1  Added fix to raw HTML not being stored, thanks
///									for this go to Christopher Woodill <chriswoodill@yahoo.com>
///					1/10/05 v1.0.0	Public release
///					  ...			Many changes here
///					4/08/04 v0.5.0	New
///							 
/// 
/// 
/// </summary>
namespace Majestic12
{

	/// <summary>
	/// Type of parsed HTML chunk (token)
	/// </summary>
	public enum HTMLchunkType
	{
		/// <summary>
		/// Text data from HTML
		/// </summary>
		Text		= 0,
		/// <summary>
		/// Open tag, ie <b>
		/// </summary>
		OpenTag		= 1,
		/// <summary>
		/// Closed data, ie </b>
		/// </summary>
		CloseTag	= 2,
		/// <summary>
		/// Data between HTML comments, ie: <!-- -->
		/// </summary>
		Comment		= 3,
	};

	/// <summary>
	/// Class for fast dynamic string building - it is faster than StringBuilder
	/// </summary>
#if UNSAFE_CODE
	public unsafe class DynaString
#else
	public class DynaString
#endif
	{
		/// <summary>
		/// Finalised text will be available in this string
		/// </summary>
		public string sText;

		/// <summary>
		/// CRITICAL: that much capacity will be allocated (once) for this object -- for performance reasons
		/// we do NOT have range checks because we make reasonably safe assumption that accumulated string will
		/// fit into the buffer. If you have very abnormal strings then you should increase buffer accordingly.
		/// </summary>
		public static int TEXT_CAPACITY=1024*128-1;

		public byte[] bBuffer;
		public int iBufPos;
		private int Length;

		/// <summary>
		/// Constructor 
		/// </summary>
		/// <param name="sString">Initial string</param>
		public DynaString(string sString)
		{
			sText=sString;
			iBufPos=0;
			bBuffer=new byte[TEXT_CAPACITY+1];
			Length=sString.Length;
		}

		/// <summary>
		/// Converts data in buffer to string
		/// </summary>
		/// <returns>String</returns>
		public override string ToString()
		{
			if(iBufPos>0)
				Finalise();
			
			return sText;
		}

		/// <summary>
		/// Lower cases data in buffer and returns as string. 
		/// WARNING: This is only to be used for ASCII lowercasing - HTML params and tags, not their values
		/// </summary>
		/// <returns>String that is now accessible via sText</returns>
		public string ToLowerString()
		{
			if(sText.Length==0)
			{	

#if UNSAFE_CODE			
				// lower case it directly in the buffer
				fixed(byte* pBuffer=bBuffer)
				{
					int i=0; 
					for(byte *p=pBuffer; i<iBufPos; i++, p++)
					{
						if(*p>=65 && *p<=90)
							*p=(byte)(*p+32);
					}
					
				}

				ToString();
#else
			ToString();
			sText=sText.ToLower();
#endif
			}
			else
			{
				ToString();
			
#if UNSAFE_CODE			
			// fast in place conversion to lower case text

			fixed (char *pfixed = sText)
				for (char *p=pfixed; *p != 0; p++)
					*p = char.ToLower(*p);
#else
			sText=sText.ToLower();
#endif
			}

			return sText;
		}

		/// <summary>
		/// Resets object to zero length string
		/// </summary>
		public void Clear()
		{
			sText="";
			Length=0;
			iBufPos=0;
		}

		/// <summary>
		/// Appends a "char" to the buffer
		/// </summary>
		/// <param name="cChar">Appends char (byte really)</param>
		public void Append(byte cChar)
		{
			// Length++;

			if(iBufPos>=TEXT_CAPACITY)
			{
				if(sText.Length==0)
				{
#if UNSAFE_CODE
					fixed(byte* pBuffer=bBuffer)
					{
						sText=new String((sbyte*)pBuffer,0,iBufPos,System.Text.Encoding.Default);
					}
#else	
					
					sText=Encoding.Default.GetString(bBuffer,0,iBufPos);
#endif
				}
				else
					//sText+=new string(bBuffer,0,iBufPos);
					sText+=Encoding.Default.GetString(bBuffer,0,iBufPos);

				Length+=iBufPos;

				iBufPos=0;
			}

			bBuffer[iBufPos++]=cChar;
		}
		
		/// <summary>
		/// Internal finaliser - creates string from data accumulated in buffer
		/// </summary>
		private void Finalise()
		{
			if(iBufPos>0)
			{
				if(sText.Length==0)
				{
#if UNSAFE_CODE	
					fixed(byte* pBuffer=bBuffer)
					{
						sText=new String((sbyte*)pBuffer,0,iBufPos,System.Text.Encoding.Default);
					}
#else
					sText=Encoding.Default.GetString(bBuffer,0,iBufPos);
#endif
				}
				else
					//sText+=new string(bBuffer,0,iBufPos);
					sText+=Encoding.Default.GetString(bBuffer,0,iBufPos);

				Length+=iBufPos;
				iBufPos=0;
			}
		}
		

	}

	/// <summary>
	/// This class will contain an HTML chunk -- parsed token that is either text, comment, open or closed tag.
	/// </summary>
#if UNSAFE_CODE
	public unsafe class HTMLchunk
#else
	public class HTMLchunk
#endif
	{	
		/// <summary>
		/// Maximum default capacity of buffer that will keep data
		/// </summary>
		public static int TEXT_CAPACITY=1024*128;

		/// <summary>
		/// Maximum number of parameters in a tag - should be high enough to fit most
		/// </summary>
		public static int MAX_PARAMS=256;	

		/// <summary>
		/// Chunk type showing whether its text, open or close tag, or comments.
		/// </summary>
		public HTMLchunkType oType;

        // was getting outofmemoryexceptions
        public byte[] bBuffer = new byte[10000];//new byte[TEXT_CAPACITY+1];
		public int iBufPos=0;

		public int iHTMLen=0;

		/// <summary>
		/// If true then tag params will be kept in a hash rather than in a fixed size arrays. This will be slow
		/// down parsing, but make it easier to use
		/// </summary>
		public bool bHashMode=true;

		/// <summary>
		/// For TAGS: it stores raw HTML that was parsed to generate thus chunk will be here UNLESS
		/// HTMLparser was configured not to store it there as it can improve performance
		/// 
		/// For TEXT or COMMENTS: actual text or comments
		/// </summary>
		public string oHTML="";

		/// <summary>
		/// If its open/close tag type then this is where lowercased Tag will be kept
		/// </summary>
		public string sTag="";

		public bool bClosure=false;
		public bool bComments=false;
		
		/// <summary>
		/// True if entities were present (and transformed) in the original HTML
		/// </summary>
		public bool bEntities=false;

		/// <summary>
		/// Set to true if &lt; entity (< tag start) was found 
		/// </summary>
 		public bool bLtEntity=false;

		/// <summary>
		/// Hashtable with tag parameters: keys are param names and values are param values.
		/// ONLY used if bHashMode is set to TRUE.
		/// </summary>
		public Hashtable oParams=null; 

		/// <summary>
		/// Number of parameters and values stored in sParams array.
		/// ONLY used if bHashMode is set to FALSE.
		/// </summary>
		public int iParams=0;

		/// <summary>
		/// Param names will be stored here - actual number is in iParams.
		/// ONLY used if bHashMode is set to FALSE.
		/// </summary>
		public string[] sParams=new string[MAX_PARAMS];

		/// <summary>
		/// Param values will be stored here - actual number is in iParams.
		/// ONLY used if bHashMode is set to FALSE.
		/// </summary>
		public string[] sValues=new string[MAX_PARAMS];

		/// <summary>
		/// This function will convert parameters stored in sParams/sValues arrays into oParams hash
		/// Useful if generally parsing is done when bHashMode is FALSE. Hash operations are not the fastest, so
		/// its best not to use this function.
		/// </summary>
		public void ConvertParamsToHash()
		{
			if(oParams!=null)
				oParams.Clear();
			else
				oParams=new Hashtable();

			for(int i=0; i<iParams; i++)
			{
				oParams[sParams[i]]=sValues[i];
			}

		}

		/// <summary>
		/// Adds tag parameter to the chunk
		/// </summary>
		/// <param name="sParam">Parameter name (ie color)</param>
		/// <param name="sValue">Value of the parameter (ie white)</param>
		public void AddParam(string sParam,string sValue)
		{
			if(!bHashMode)
			{

				if(iParams<MAX_PARAMS)
				{
					sParams[iParams]=sParam;
					sValues[iParams]=sValue;

					iParams++;
				}
			}
			else
			{
				oParams[sParam]=sValue;
			}

		}

		~HTMLchunk()
		{
			Close();
		}

		/// <summary>
		/// Closes chunk trying to reclaims memory used by buffers
		/// </summary>
		public void Close()
		{
			if(oParams!=null)
				oParams=null;
			
			bBuffer=null;
		}

		/// <summary>
		/// Clears chunk preparing it for 
		/// </summary>
		public void Clear()
		{
			iHTMLen=iBufPos=0;
			//oHTML=null;
			sTag=oHTML="";
			//sTag=null;
			//sTag="";
			bLtEntity=bEntities=bComments=bClosure=false;
			
			/*
			bComments=false;
			bEntities=false;
			bLtEntity=false;
			*/

			if(!bHashMode)
			{
				/*
				for(int i=0; i<iParams; i++)
				{
					sParams[i]=null;
					sValues[i]=null;
				}
				*/

				iParams=0;
			}
			else
			{
				if(oParams!=null)
					oParams.Clear();
			}
			//if(oParams.Count>0)
			//	oParams=new Hashtable();
		}

		/// <summary>
		/// Initialises new HTMLchunk
		/// </summary>
		/// <param name="p_bHashMode">Sets <seealso cref="bHashMode"/></param>
		public HTMLchunk(bool p_bHashMode)
		{
			bHashMode=p_bHashMode;
			
			if(bHashMode)
				oParams=new Hashtable();
		}

		/// <summary>
		/// Appends char to chunk
		/// </summary>
		/// <param name="cChar">Char (byte really)</param>
		public void Append(byte cChar)
		{
			// no range check here for performance reasons - this function gets called VERY often

			bBuffer[iBufPos++]=cChar;
		}

		/// <summary>
		/// Finalises data from buffer into HTML string
		/// </summary>
		public void Finalise()
		{
			if(oHTML.Length==0)
			{
				if(iBufPos>0)
				{
#if UNSAFE_CODE
					fixed(byte* pBuffer=bBuffer)
					{
						oHTML=new String((sbyte*)pBuffer,0,iBufPos,System.Text.Encoding.Default);
					}
#else
					oHTML=Encoding.Default.GetString(bBuffer,0,iBufPos);
#endif
				}
			}
			else
			{
				if(iBufPos>0)
					//oHTML+=new string(bBuffer,0,iBufPos);
					oHTML+=Encoding.Default.GetString(bBuffer,0,iBufPos);
			}

			iHTMLen+=iBufPos;
			iBufPos=0;
		}

	}

	/// <summary>
	/// HTMLparser -- class that allows to parse HTML split into tokens
	/// It might just be Unicode compatible as well... 
	/// </summary>
	public class HTMLparser
	{
		/// <summary>
		/// If true the text will be returned as split words. 
		/// Performance hint: keep it as false
		/// </summary>
		public bool bReturnSplitWords=false;

		/// <summary>
		/// If true then parsed tag chunks will contain raw HTML, otherwise only comments will have it
		/// Performance hint: keep it as false
		/// </summary>
		public bool bKeepRawHTML=false;

		/// <summary>
		/// If not true then HTML entities (like &nbsp) won't be decoded
		/// </summary>
		public bool bDecodeEntities=false;

		/// <summary>
		/// Internal -- dynamic string for text accumulation
		/// </summary>
		DynaString sText=new DynaString("");

		/// <summary>
		/// This chunk will be returned when it was parsed
		/// </summary>
		HTMLchunk oChunk=new HTMLchunk(true);

		/// <summary>
		/// Support for unicode is rudimentary at best, not sure if 
		/// it actually works, this switch however will turn on and off some
		/// known bits that can introduce Unicode characters into otherwise
		/// virgin perfection of ASCII.
		/// 
		/// Currently this flag will only effect parsing of unicode HTML entities
		/// </summary>
		bool bUniCodeSupport=true;

		// if true nested comments (<!-- <!-- --> -->) will be understood
		//bool bNestedComments=false;

		//byte[] cHTML=null;

		/// <summary>
		/// Byte array with HTML will be kept here
		/// </summary>
		byte[] bHTML;

		/// <summary>
		/// Internal - current position pointing to byte in bHTML
		/// </summary>
		int iCurPos=0;

		/// <summary>
		/// Length of bHTML -- it appears to be faster to use it than bHTML.Length
		/// </summary>
		int iDataLength=0;

		/// <summary>
		/// Supported HTML entities
		/// </summary>
		private Hashtable oEntities=null;

		/// <summary>
		/// If false then text will be ignored -- it will make parser run faster but in effect only HTML tags 
		/// will be returned, its in effect only useful when you just want to parse links or something like this
		/// </summary>
		public bool bTextMode=true;

		/// <summary>
		/// Internal heuristics for entiries: these will be set to min and max string lengths of known HTML entities
		/// </summary>
		int iMinEntityLen=0,iMaxEntityLen=0;

		/// <summary>
		/// Array to provide reverse lookup for entities
		/// </summary>
		string[] sEntityReverseLookup;
			

		public HTMLparser()
		{
			InitEntities();	
		}

		public void SetDecodeEntitiesMode(bool bMode)
		{
			bDecodeEntities=bMode;
		}

		public void SetChunkHashMode(bool bHashMode)
		{
			oChunk.bHashMode=bHashMode;
		}

		~HTMLparser()
		{
			Close();	
		}

		/// <summary>
		/// 
		/// </summary>
		public void Close()
		{
			if(bHTML!=null)
				bHTML=null;

			if(oEntities!=null)
			{
				oEntities.Clear();
				oEntities=null;
			}
		}

		/// <summary>
		/// Sets encoding -- NOT USED AT THE MOMENT
		/// </summary>
		/// <param name="sCharset">Encoding Charset</param>
		public void SetEncoding(string sCharset)
		{
			/*
			if(oCharset.IsSupported(sCharset))
			{
				oCharset.SetCharset(sCharset);
				//bCharConv=true;
			}
			//else Console.WriteLine("Charset '{0}' is not supported!",sCharset);
			*/
		}


		/// <summary>
		/// Initialises list of entities
		/// </summary>
		private void InitEntities()
		{
			oEntities=new Hashtable(200);

			// FIXIT: we will treat non-breakable space... as space!?!
			// perhaps it would be better to have separate return types for entities?
			oEntities.Add("nbsp",32); //oEntities.Add("nbsp",160);
			oEntities.Add("iexcl",161);
			oEntities.Add("cent",162);
			oEntities.Add("pound",163);
			oEntities.Add("curren",164);
			oEntities.Add("yen",165);
			oEntities.Add("brvbar",166);
			oEntities.Add("sect",167);
			oEntities.Add("uml",168);
			oEntities.Add("copy",169);
			oEntities.Add("ordf",170);
			oEntities.Add("laquo",171);
			oEntities.Add("not",172);
			oEntities.Add("shy",173);
			oEntities.Add("reg",174);
			oEntities.Add("macr",175);
			oEntities.Add("deg",176);
			oEntities.Add("plusmn",177);
			oEntities.Add("sup2",178);
			oEntities.Add("sup3",179);
			oEntities.Add("acute",180);
			oEntities.Add("micro",181);
			oEntities.Add("para",182);
			oEntities.Add("middot",183);
			oEntities.Add("cedil",184);
			oEntities.Add("sup1",185);
			oEntities.Add("ordm",186);
			oEntities.Add("raquo",187);
			oEntities.Add("frac14",188);
			oEntities.Add("frac12",189);
			oEntities.Add("frac34",190);
			oEntities.Add("iquest",191);
			oEntities.Add("Agrave",192);
			oEntities.Add("Aacute",193);
			oEntities.Add("Acirc",194);
			oEntities.Add("Atilde",195);
			oEntities.Add("Auml",196);
			oEntities.Add("Aring",197);
			oEntities.Add("AElig",198);
			oEntities.Add("Ccedil",199);
			oEntities.Add("Egrave",200);
			oEntities.Add("Eacute",201);
			oEntities.Add("Ecirc",202);
			oEntities.Add("Euml",203);
			oEntities.Add("Igrave",204);
			oEntities.Add("Iacute",205);
			oEntities.Add("Icirc",206);
			oEntities.Add("Iuml",207);
			oEntities.Add("ETH",208);
			oEntities.Add("Ntilde",209);
			oEntities.Add("Ograve",210);
			oEntities.Add("Oacute",211);
			oEntities.Add("Ocirc",212);
			oEntities.Add("Otilde",213);
			oEntities.Add("Ouml",214);
			oEntities.Add("times",215);
			oEntities.Add("Oslash",216);
			oEntities.Add("Ugrave",217);
			oEntities.Add("Uacute",218);
			oEntities.Add("Ucirc",219);
			oEntities.Add("Uuml",220);
			oEntities.Add("Yacute",221);
			oEntities.Add("THORN",222);
			oEntities.Add("szlig",223);
			oEntities.Add("agrave",224);
			oEntities.Add("aacute",225);
			oEntities.Add("acirc",226);
			oEntities.Add("atilde",227);
			oEntities.Add("auml",228);
			oEntities.Add("aring",229);
			oEntities.Add("aelig",230);
			oEntities.Add("ccedil",231);
			oEntities.Add("egrave",232);
			oEntities.Add("eacute",233);
			oEntities.Add("ecirc",234);
			oEntities.Add("euml",235);
			oEntities.Add("igrave",236);
			oEntities.Add("iacute",237);
			oEntities.Add("icirc",238);
			oEntities.Add("iuml",239);
			oEntities.Add("eth",240);
			oEntities.Add("ntilde",241);
			oEntities.Add("ograve",242);
			oEntities.Add("oacute",243);
			oEntities.Add("ocirc",244);
			oEntities.Add("otilde",245);
			oEntities.Add("ouml",246);
			oEntities.Add("divide",247);
			oEntities.Add("oslash",248);
			oEntities.Add("ugrave",249);
			oEntities.Add("uacute",250);
			oEntities.Add("ucirc",251);
			oEntities.Add("uuml",252);
			oEntities.Add("yacute",253);
			oEntities.Add("thorn",254);
			oEntities.Add("yuml",255);
			oEntities.Add("quot",34);
			oEntities.Add("amp",38);
			oEntities.Add("lt",60);
			oEntities.Add("gt",62);

			if(bUniCodeSupport)
			{
				oEntities.Add("OElig",338);
				oEntities.Add("oelig",339);
				oEntities.Add("Scaron",352);
				oEntities.Add("scaron",353);
				oEntities.Add("Yuml",376);
				oEntities.Add("circ",710);
				oEntities.Add("tilde",732);
				oEntities.Add("ensp",8194);
				oEntities.Add("emsp",8195);
				oEntities.Add("thinsp",8201);
				oEntities.Add("zwnj",8204);
				oEntities.Add("zwj",8205);
				oEntities.Add("lrm",8206);
				oEntities.Add("rlm",8207);
				oEntities.Add("ndash",8211);
				oEntities.Add("mdash",8212);
				oEntities.Add("lsquo",8216);
				oEntities.Add("rsquo",8217);
				oEntities.Add("sbquo",8218);
				oEntities.Add("ldquo",8220);
				oEntities.Add("rdquo",8221);
				oEntities.Add("bdquo",8222);
				oEntities.Add("dagger",8224);
				oEntities.Add("Dagger",8225);
				oEntities.Add("permil",8240);
				oEntities.Add("lsaquo",8249);
				oEntities.Add("rsaquo",8250);
				oEntities.Add("euro",8364);
				oEntities.Add("fnof",402);
				oEntities.Add("Alpha",913);
				oEntities.Add("Beta",914);
				oEntities.Add("Gamma",915);
				oEntities.Add("Delta",916);
				oEntities.Add("Epsilon",917);
				oEntities.Add("Zeta",918);
				oEntities.Add("Eta",919);
				oEntities.Add("Theta",920);
				oEntities.Add("Iota",921);
				oEntities.Add("Kappa",922);
				oEntities.Add("Lambda",923);
				oEntities.Add("Mu",924);
				oEntities.Add("Nu",925);
				oEntities.Add("Xi",926);
				oEntities.Add("Omicron",927);
				oEntities.Add("Pi",928);
				oEntities.Add("Rho",929);
				oEntities.Add("Sigma",931);
				oEntities.Add("Tau",932);
				oEntities.Add("Upsilon",933);
				oEntities.Add("Phi",934);
				oEntities.Add("Chi",935);
				oEntities.Add("Psi",936);
				oEntities.Add("Omega",937);
				oEntities.Add("alpha",945);
				oEntities.Add("beta",946);
				oEntities.Add("gamma",947);
				oEntities.Add("delta",948);
				oEntities.Add("epsilon",949);
				oEntities.Add("zeta",950);
				oEntities.Add("eta",951);
				oEntities.Add("theta",952);
				oEntities.Add("iota",953);
				oEntities.Add("kappa",954);
				oEntities.Add("lambda",955);
				oEntities.Add("mu",956);
				oEntities.Add("nu",957);
				oEntities.Add("xi",958);
				oEntities.Add("omicron",959);
				oEntities.Add("pi",960);
				oEntities.Add("rho",961);
				oEntities.Add("sigmaf",962);
				oEntities.Add("sigma",963);
				oEntities.Add("tau",964);
				oEntities.Add("upsilon",965);
				oEntities.Add("phi",966);
				oEntities.Add("chi",967);
				oEntities.Add("psi",968);
				oEntities.Add("omega",969);
				oEntities.Add("thetasym",977);
				oEntities.Add("upsih",978);
				oEntities.Add("piv",982);
				oEntities.Add("bull",8226);
				oEntities.Add("hellip",8230);
				oEntities.Add("prime",8242);
				oEntities.Add("Prime",8243);
				oEntities.Add("oline",8254);
				oEntities.Add("frasl",8260);
				oEntities.Add("weierp",8472);
				oEntities.Add("image",8465);
				oEntities.Add("real",8476);
				oEntities.Add("trade",8482);
				oEntities.Add("alefsym",8501);
				oEntities.Add("larr",8592);
				oEntities.Add("uarr",8593);
				oEntities.Add("rarr",8594);
				oEntities.Add("darr",8595);
				oEntities.Add("harr",8596);
				oEntities.Add("crarr",8629);
				oEntities.Add("lArr",8656);
				oEntities.Add("uArr",8657);
				oEntities.Add("rArr",8658);
				oEntities.Add("dArr",8659);
				oEntities.Add("hArr",8660);
				oEntities.Add("forall",8704);
				oEntities.Add("part",8706);
				oEntities.Add("exist",8707);
				oEntities.Add("empty",8709);
				oEntities.Add("nabla",8711);
				oEntities.Add("isin",8712);
				oEntities.Add("notin",8713);
				oEntities.Add("ni",8715);
				oEntities.Add("prod",8719);
				oEntities.Add("sum",8721);
				oEntities.Add("minus",8722);
				oEntities.Add("lowast",8727);
				oEntities.Add("radic",8730);
				oEntities.Add("prop",8733);
				oEntities.Add("infin",8734);
				oEntities.Add("ang",8736);
				oEntities.Add("and",8743);
				oEntities.Add("or",8744);
				oEntities.Add("cap",8745);
				oEntities.Add("cup",8746);
				oEntities.Add("int",8747);
				oEntities.Add("there4",8756);
				oEntities.Add("sim",8764);
				oEntities.Add("cong",8773);
				oEntities.Add("asymp",8776);
				oEntities.Add("ne",8800);
				oEntities.Add("equiv",8801);
				oEntities.Add("le",8804);
				oEntities.Add("ge",8805);
				oEntities.Add("sub",8834);
				oEntities.Add("sup",8835);
				oEntities.Add("nsub",8836);
				oEntities.Add("sube",8838);
				oEntities.Add("supe",8839);
				oEntities.Add("oplus",8853);
				oEntities.Add("otimes",8855);
				oEntities.Add("perp",8869);
				oEntities.Add("sdot",8901);
				oEntities.Add("lceil",8968);
				oEntities.Add("rceil",8969);
				oEntities.Add("lfloor",8970);
				oEntities.Add("rfloor",8971);
				oEntities.Add("lang",9001);
				oEntities.Add("rang",9002);
				oEntities.Add("loz",9674);
				oEntities.Add("spades",9824);
				oEntities.Add("clubs",9827);
				oEntities.Add("hearts",9829);
				oEntities.Add("diams",9830);
			}

			sEntityReverseLookup=new string[10000];

			// calculate min/max lenght of known entities
			foreach(string sKey in oEntities.Keys)
			{
				if(sKey.Length<iMinEntityLen || iMinEntityLen==0)
					iMinEntityLen=sKey.Length;

				if(sKey.Length>iMaxEntityLen || iMaxEntityLen==0)
					iMaxEntityLen=sKey.Length;

				// remember key at given offset
				sEntityReverseLookup[(int)oEntities[sKey]]=sKey;
			}

			// we don't want to change spaces
			sEntityReverseLookup[32]=null;
		}

		/// <summary>
		/// Parses line and changes known entiry characters into proper HTML entiries
		/// </summary>
		/// <param name="sLine">Line of text</param>
		/// <returns>Line of text with proper HTML entities</returns>
		public string ChangeToEntities(string sLine)
		{
			StringBuilder oSB=new StringBuilder(sLine.Length);

			for(int i=0; i<sLine.Length; i++)
			{				
				char cChar=sLine[i];

				// yeah I know its lame but its 3:30am and I had v.long debugging session :-/
				switch((int)cChar)
				{
					case 39:
					case 145:
					case 146:
					case 147:
					case 148:
						oSB.Append("&#"+((int)cChar).ToString()+";");
						continue;

					default:
						break;
				};

				if(cChar<sEntityReverseLookup.Length)
				{
		
					if(sEntityReverseLookup[(int)cChar]!=null)
					{
						oSB.Append("&");
						oSB.Append(sEntityReverseLookup[(int)cChar]);
						oSB.Append(";");
						continue;
					}
				}

				oSB.Append(cChar);
			}		

			return oSB.ToString();
		}

		/// <summary>
		/// Constructs parser object using provided HTML as source for parsing
		/// </summary>
		/// <param name="p_oHTML"></param>
		public HTMLparser(string p_oHTML)
		{
			Init(p_oHTML);
		}

		/// <summary>
		/// Sets text treatment mode 
		/// </summary>
		/// <param name="p_bTextMode">If TRUE, then text will be parsed, if FALSE then it will be ignored (parsing of tags will be faster however)</param>
		public void SetTextMode(bool p_bTextMode)
		{
			bTextMode=p_bTextMode;
		}


		/// <summary>
		/// Initialises parses with HTML to be parsed from provided string
		/// </summary>
		/// <param name="p_oHTML">String with HTML in it</param>
		public void Init(string p_oHTML)
		{
			Init(Encoding.Default.GetBytes(p_oHTML));
		}

		/// <summary>
		/// Initialises parses with HTML to be parsed from provided data buffer
		/// </summary>
		/// <param name="p_bHTML">Data buffer with HTML in it</param>
		public void Init(byte[] p_bHTML)
		{
			CleanUp();
			bHTML=p_bHTML;
			iDataLength=bHTML.Length;
		}

		/// <summary>
		/// Cleans up parser in preparation for next parsing
		/// </summary>
		public void CleanUp()
		{
			if(oEntities==null)
				InitEntities();

			bHTML=null;

			iCurPos=0;
			iDataLength=0;
		}

		/// <summary>
		/// Resets current parsed data to start
		/// </summary>
		public void Reset()
		{
			iCurPos=0;
		}

		/// <summary>
		/// Internal: parses tag that started from current position
		/// </summary>
		/// <param name="bKeepWhiteSpace">If true then whitespace will be kept, if false then it won't be (faster option)</param>
		/// <returns>HTMLchunk with tag information</returns>
#if UNSAFE_CODE
		private unsafe HTMLchunk ParseTag(bool bKeepWhiteSpace)
#else
		private HTMLchunk ParseTag(bool bKeepWhiteSpace)
#endif
		{
			/*
			 *  WARNING: this code was optimised for performance rather than for readability, 
			 *  so be extremely careful at changing it -- your changes could easily result in wrongly parsed HTML
			 * 
			 *  This routine takes about 60% of CPU time, in theory its the best place to gain extra speed,
			 *  but I've spent plenty of time doing it, so it won't be easy... and if is easy then please post
			 *  your changes for everyone to enjoy!
			 * 
			 * 
			 * */


			sText.Clear();

			//oChunk.Clear();

			bool bWhiteSpace=false;
			bool bComments=false;
			
			// for tracking quotes purposes
			bool bQuotes=false;
			byte cQuotes=0x20;
			//bool bParamValue=false;
			byte cChar=0;
			byte cPeek;

			// if true it means we have parsed complete tag
			bool bGotTag=false;

			string sParam="";
			int iEqualIdx=0;

			//bool bQuotesAllowed=false;

			//StringBuilder sText=new StringBuilder(128);
			//,HTMLchunk.TEXT_CAPACITY
			
			// we reach this function immediately after tag's byte (<) was
			// detected, so we need to save it in order to keep correct HTML copy
			oChunk.Append((byte)'<'); // (byte)'<'
			
			/*
			oChunk.bBuffer[0]=60;
			oChunk.iBufPos=1;
			oChunk.iHTMLen=1;
			*/

			//while(!Eof())
			//int iTagLength=0;

			while(iCurPos<iDataLength)
			{
				// we will only skip whitespace OUTSIDE of quotes and comments
				bWhiteSpace=false;

				if(!bQuotes && !bComments && !bKeepWhiteSpace)
				{
					//bWhiteSpace=SkipWhiteSpace();

					while(iCurPos<iDataLength)
					{
						cChar=bHTML[iCurPos++];

						if(cChar!=' ' && cChar!='\t' && cChar!=13 && cChar!=10)
						//if(!char.IsWhiteSpace((char)cChar))
						{
							//PutChar();
							//iCurPos--;
							break;
						}
						else
							bWhiteSpace=true;
					}

					if(iCurPos>=iDataLength)
						cChar=0;
					
					//cChar=NextChar();

					if(bWhiteSpace && (bKeepRawHTML || !bGotTag || (bGotTag && bComments)))
						oChunk.Append((byte)' ');
				}
				else
				{
					//cChar=NextChar();
					cChar=bHTML[iCurPos++];

					
					if(cChar==' ' || cChar=='\t' || cChar==10 || cChar==13)
					{
						bWhiteSpace=true;

						// we don't want that nasty unnecessary 0x0D or 13 byte :-/
						if(cChar!=13 && (bKeepWhiteSpace || bComments || bQuotes))
						{
							if(bKeepRawHTML || !bGotTag || (bGotTag && bComments))
								oChunk.Append(cChar);

							//sText.Append(cChar);
							// NOTE: this is manual inlining from actual object
							// NOTE: speculative execution that requires large enough buffer to avoid overflowing it
							/*
							if(sText.iBufPos>=DynaString.TEXT_CAPACITY)
							{
								sText.Append(cChar);
							}
							else
							*/
							{
								//sText.Length++;
								sText.bBuffer[sText.iBufPos++]=cChar;
							}

							continue;
						}
					}

				}
			
				//if(cChar==0)
				//	break;

				//if(cChar==13 || cChar==10)
				//	continue;
							
				// check if its entity
				//if(cChar=='&')
				if(cChar==38 && !bComments)
				{
					cChar=(byte)CheckForEntity();

					// restore current symbol
					if(cChar==0)
						cChar=38; //(byte)'&';
					else
					{
						// we have to skip now to next byte, since 
						// some converted chars might well be control chars like >
						oChunk.bEntities=true;

						if(cChar=='<')
							oChunk.bLtEntity=true;
						
						// unless is space we will ignore it
						// note that this won't work if &nbsp; is defined as it should
						// byte int value of 160, rather than 32.
						//if(cChar!=' ')
						oChunk.Append(cChar);

						//continue;
					}
				}

				// cPeek=Peek();
				
				if(iCurPos<iDataLength)
					cPeek=bHTML[iCurPos];
				else
					cPeek=0;
					
				// check if we've got tag now: either whitespace before current symbol or the next one is end of string
				if(!bGotTag)
				{
					oChunk.Append(cChar);

					if((sText.iBufPos>=3 && (sText.bBuffer[0]=='!' && sText.bBuffer[1]=='-' && sText.bBuffer[2]=='-')))
						bComments=true;

					if(bWhiteSpace || (!bWhiteSpace && cPeek=='>')  || cPeek==0 || bComments)
						
						//|| sText.sText=="!--") || sText.ToString()=="!--"))	//(sText.bBuffer[0]=='!' || sText.sText=="!--") &&
					{
						if(cPeek=='>' && cChar!='/')
							sText.Append(cChar);

						if(bComments)
						{
							oChunk.sTag="!--";
							oChunk.oType=HTMLchunkType.Comment;
							oChunk.bComments=true;
						}
						else
						{
							oChunk.sTag=sText.ToLowerString();
						}

						bGotTag=true;

						//sText.Remove(0,sText.Length);
						sText.Clear();
					}
				}
				else
				{
					if(bKeepRawHTML || bComments)
						oChunk.Append(cChar);

					// ought to be parameter
					if(sText.iBufPos!=0 && !oChunk.bComments)
					{
						if((bWhiteSpace && !bQuotes) || (cPeek==0 || cPeek=='>'))
						{
							if(cPeek=='>' && cChar!='/' && cQuotes!=cChar)
								sText.Append(cChar);
				
							// some params consist of key=value combination
							sParam=sText.ToString();

#if UNSAFE_CODE
							iEqualIdx=-1;

							// scan string to identify position of = that separates param name and param value
							fixed(char *pfixed = sParam)
							{
								//int iEqual=-1;
								int iPos=0;

								for(char *p=pfixed; *p!= 0; p++,iPos++)
								{
									if(iEqualIdx==-1)
									{
										// anything before = will be lower cased in place
										// this lower casing code will work for ASCII only -- should not be
										// used for anything apart from parameters and tags -- not param values
										if(*p>=65 && *p<=90)
										{
											*p=(char)((*p)+32);
											//*p=char.ToLower(*p);
										}
										else
										{
											if(*p=='=')
											{
												if(iPos>0)
													oChunk.AddParam(new String(pfixed,0,iPos),new String(p+1,0,sParam.Length-iPos-1));
												iEqualIdx=iPos;
												break;
											}
										}
											
									}
								}


							}

							if(iEqualIdx<=0)
							{
								oChunk.AddParam(sParam,"");
							}
#else
							iEqualIdx=sParam.IndexOf('=');

							//bQuotesAllowed=false;

							if(iEqualIdx<=0)
							{
								/*
#if UNSAFE_CODE
								Utils.FastToLower(sParam);
								oChunk.AddParam(sParam,"");
#else
								oChunk.AddParam(sParam.ToLower(),"");
#endif
*/
								oChunk.AddParam(sParam.ToLower(),"");
							}
							else
							{
								oChunk.AddParam(sParam.Substring(0,iEqualIdx).ToLower(),sParam.Substring(iEqualIdx+1,sParam.Length-iEqualIdx-1));
								//bQuotesAllowed=true;
							}
#endif						
							
							//sText.Remove(0,sText.Length);
							sText.Clear();

							sParam=null;
						}

					}
				}

				switch((byte)cChar)
				{
					case 0:
						goto GetOut;
							
					//case (byte)'>':
					case 62:

						//bQuotesAllowed=false;
						// if we are in comments mode then we will be waiting for -->
						if(bComments)
						{
							if(LookBack(2)=='-' && LookBack(3)=='-')
							{
								bComments=false;
								return oChunk;
							}
						}
						else
						{
							if(!bQuotes)
							{
								if(oChunk.bComments)
									oChunk.oType=HTMLchunkType.Comment;
								else
								{
									if(oChunk.bClosure)
										oChunk.oType=HTMLchunkType.CloseTag;
									else
										oChunk.oType=HTMLchunkType.OpenTag;
								}
								return oChunk;
							}
						}

						break;

					//case (byte)'"':
					case 34:
					//case (byte)'\'':
					case 39:

						if(bQuotes)
						{
							if(cQuotes==cChar)// && bQuotesAllowed)
							{
								bQuotes=false;
								//bQuotesAllowed=false;
							}
							else
								goto AddSymbol;
						}
						else
						{
							//if(bQuotesAllowed)
							{
								bQuotes=true;
								cQuotes=(byte)cChar;
							}
						}

						break;

					//case (byte)'/':
					case 47:

						if(!bQuotes && !bGotTag)
							oChunk.bClosure=true;
						else
							goto AddSymbol;
	
						break;
						
					default:
					
						AddSymbol:
							
						//if(bWhiteSpace && bQuotes)
						//	sText.Append(0x20);

						//sText.Append(cChar);
							// NOTE: this is manual inlining from actual object

						// NOTE: we go here for speculative insertion that expectes that we won't run out of
						// buffer, which should be big enough to hold most of HTML data.
						/*
							if(sText.iBufPos>=DynaString.TEXT_CAPACITY)
							{
								sText.Append(cChar);
							}
							else
						*/
							{
								//sText.Length++;
								sText.bBuffer[sText.iBufPos++]=cChar;
							}


						break;
				};

			}

			GetOut:

			if(oChunk.bComments)
				oChunk.oType=HTMLchunkType.Comment;
			else
			{
				if(oChunk.bClosure)
					oChunk.oType=HTMLchunkType.CloseTag;
				else
					oChunk.oType=HTMLchunkType.OpenTag;
			}

			return oChunk;
		}

		/// <summary>
		/// Returns true if we reached end of data
		/// </summary>
		/// <returns>True if we reached data end</returns>
		private bool Eof()
		{
			if(iCurPos>=iDataLength)
				return true;

			return false;
		}

		/// <summary>
		/// Peeks forward and returns next byte ahead, 0 if its end of data
		/// </summary>
		/// <returns>Next byte in data, or 0 if its end</returns>
		private byte Peek()
		{
			//if(Eof())
			if(iCurPos>=iDataLength)
				return 0;

			return GetChar(iCurPos);
		}

		/// <summary>
		/// Looks back and returns char that was there, or 0 if its start
		/// </summary>
		/// <returns>Previous byte, or 0 if we are at start position</returns>
		private byte LookBack()
		{
			//try
			//{
				if(iCurPos>0)
					return GetChar(iCurPos-1);
			//}
			//catch
			//{
			
				return (byte)(0);
			//}
			//return LookBack(1);
		}

		/// <summary>
		/// Looks back X bytes and returns char that was there, or 0 if its start
		/// </summary>
		/// <returns>Previous byte, or 0 if we will reached start position</returns>
		private byte LookBack(int iHowFar)
		{
			if(iCurPos>=iHowFar)
				return GetChar(iCurPos-iHowFar);

			return 0;
		}

		/// <summary>
		/// Returns current byte 
		/// </summary>
		/// <returns>Current byte</returns>
		private byte GetChar()
		{
			//return bCharConv ? (byte)oCharset.ConvertByte(bHTML[iCurPos]) : (byte)bHTML[iCurPos];

			return bHTML[iCurPos];
		}

		/// <summary>
		/// Returns byte at specified position
		/// </summary>
		/// <param name="iPos">Position (WARNING: no range checks here for speed)</param>
		/// <returns>Byte at that position</returns>
		private byte GetChar(int iPos)
		{
			//return bCharConv ? (byte)oCharset.ConvertByte(bHTML[iPos]) : (byte)bHTML[iPos];
			return bHTML[iPos];
		}

		/// <summary>
		/// Puts back current byte
		/// </summary>
		private void PutChar()
		{
			if(iCurPos>0)
				iCurPos--;
		}

		/// <summary>
		/// Puts back specified number of chars (bytes really)
		/// </summary>
		/// <param name="iChars">Number of chars</param>
		private void PutChars(int iChars)
		{
			if((iCurPos-iChars)>=0)
				iCurPos-=iChars;
		}

		/// <summary>
		/// Returns next char in data and increments points
		/// </summary>
		/// <returns>Next char or 0 to indicate end of data</returns>
		private byte NextChar()
		{

			//if(Eof())
			if(iCurPos>=iDataLength)
				return 0;

			//iCurPos++;
	
			//return bCharConv ? (byte)oCharset.ConvertByte(bHTML[iCurPos-1]) : (byte)bHTML[iCurPos-1];

			return bHTML[iCurPos++];
		}

		/*
		private bool byte.IsWhiteSpace(byte cChar)
		{
			switch(cChar)
			{
				case '\n':
				case ' ':
				case '\t':
				case '\r':

					return true;

				default:

					return false;
			}
		}
		*/

		/// <summary>
		/// Skips whitespace
		/// </summary>
		/// <returns>True if any whitespace were skipped, false otherwise</returns>
		private bool SkipWhiteSpace()
		{
			byte cChar;
			bool bWhiteSpace=false;
			
			/*
			while((cChar=NextChar())!=0)
			{

				if(!char.IsWhiteSpace((char)cChar) && cChar!=13 && cChar!=10)
				{
					//PutChar();
					iCurPos--;
					return bWhiteSpace;
				}
				else
					bWhiteSpace=true;
			}
			*/
			
			while(iCurPos<iDataLength)
			{
				cChar=bHTML[iCurPos++];

				if(cChar!=' ' && cChar!='\t' && cChar!=13 && cChar!=10)
				{
					//PutChar();
					iCurPos--;
					return bWhiteSpace;
				}
				else
					bWhiteSpace=true;
			}
			
			//iCurPos++;
	
			//return bCharConv ? (byte)oCharset.ConvertByte(bHTML[iCurPos-1]) : (byte)bHTML[iCurPos-1];

			/*
			fixed(byte *bPointer=&bHTML[iCurPos])
			{
				*bPointer++=0x20;

				//return *(int*)bpPointer;
			}
			*/

			return bWhiteSpace;
		}

		
		/// <summary>
		/// Parses next chunk and returns it (whitespace is NOT kept in this version)
		/// </summary>
		/// <returns>HTMLchunk or null if end of data reached</returns>
		public HTMLchunk ParseNext()
		{
			return ParseNext(false);
		}
			
		/// <summary>
		/// Parses next chunk and returns it with 
		/// </summary>
		/// <param name="bKeepWhiteSpace">If true then whitespace will be preserved (slower)</param>
		/// <returns>HTMLchunk or null if end of data reached</returns>
		public HTMLchunk ParseNext(bool bKeepWhiteSpace)
		{
			oChunk.Clear();
			oChunk.oType=HTMLchunkType.Text;

			bool bWhiteSpace=false;
			byte cChar=0x00;

			while(true)
			{
				if(!bKeepWhiteSpace)
				{
					//bWhiteSpace=SkipWhiteSpace();

					bWhiteSpace=false;

					while(iCurPos<iDataLength)
					{
						cChar=bHTML[iCurPos++];

						if(cChar!=' ' && cChar!='\t' && cChar!=13 && cChar!=10)
						{
							// we don't do anything because we found char that can be used down the pipeline
							// without need to look it up again
							//PutChar();
							//iCurPos--;
							goto WhiteSpaceDone;
						}
						else
							bWhiteSpace=true;
					}
					
					break;

				}
				else
				{
					cChar=NextChar();

					// we are definately done
					if(cChar==0)
						break;
				}
			
			WhiteSpaceDone:		

				switch((byte)cChar)
				{
						//case '<':
					case 60:


						// we may have found text bit before getting to the tag
						// in which case we need to put back tag byte and return
						// found text first, the tag will be parsed next time
						if(oChunk.iBufPos>0 || bWhiteSpace)
						{
							// we will add 1 white space chars to compensate for 
							// loss of space before tag since this space often serves as a delimiter between words
							if(bWhiteSpace)
								oChunk.Append(0x20);

							//PutChar();
							iCurPos--;

							// finalise chunk if text mode is not false
							if(bTextMode)
								oChunk.Finalise();

							return oChunk;
						}

						if(!bKeepRawHTML)
							return ParseTag(bKeepWhiteSpace);
						else
						{
							oChunk=ParseTag(bKeepWhiteSpace);

							oChunk.Finalise();

							return oChunk;
						}

						/*
						 * case 179:
							Console.WriteLine("Found: {0} in {1}!",(char)cChar,oChunk.oHTML.ToString());
							break;
							*/
						
					case 13:
						break;

					case 10:
						if(bKeepWhiteSpace)
						{
							/*
							if(oChunk==null)
							{
								oChunk=new HTMLchunk(false);
								oChunk.oType=HTMLchunkType.Text;
							}
							*/

							oChunk.Append(cChar);
						}
						break;

					default:

						/*
						if(oChunk==null)
						{
							oChunk=new HTMLchunk(false);
							oChunk.oType=HTMLchunkType.Text;
						}
						*/
						if(bTextMode)
						{

							// check if its entity
							if(cChar=='&')
							{
								cChar=(byte)CheckForEntity();

								// restore current symbol
								if(cChar==0)
									cChar=(byte)'&';
								else
								{
									oChunk.bEntities=true;

									if(cChar=='<')
										oChunk.bLtEntity=true;
								}
							}

							if(bReturnSplitWords)
							{
								if(bWhiteSpace)
								{
									if(oChunk.iBufPos>0)
									{
										//PutChar();
										iCurPos--;

										oChunk.Finalise();
										return oChunk;
									}
								}
								else
								{
									if(char.IsPunctuation((char)cChar))
									{
										if(oChunk.iBufPos>0)
										{
											//PutChar();
											oChunk.Finalise();
											return oChunk;
										}
										else
											break;
									}
								}
							}
							else
							{
								if(bWhiteSpace && bTextMode)
									oChunk.Append((byte)' ');
							}
				
							oChunk.Append(cChar);
						}
						
						break;
				};

			}

			if(oChunk.iBufPos==0)
				return null;

			// it will be null if we have not found any data

			if(bTextMode)
				oChunk.Finalise();

			return oChunk;
		}

		

		/// <summary>
		/// This function will be called when & is found, and it will
		/// peek forward to check if its entity, should there be a success
		/// indicated by non-zero returned, the pointer will be left at the new byte
		/// after entity
		/// </summary>
		/// <returns>Char (not byte) that corresponds to the entity or 0 if it was not entity</returns>
#if UNSAFE_CODE
		private unsafe char CheckForEntity()
#else
		private char CheckForEntity()
#endif
		{
			if(!bDecodeEntities)
				return (char)0;

			int iChars=0;
			byte cChar;
			//string sEntity="";

			// if true it means we are getting hex or decimal value of the byte
			bool bCharCode=false;
			bool bCharCodeHex=false;

			int iEntLen=0;

			int iFrom=iCurPos;

			string sEntity;

			try
			{

				/*
				while(!Eof())
				{
					cChar=NextChar();
				*/
				while(iCurPos<iDataLength)
				{
					cChar=bHTML[iCurPos++];

					iChars++;
			
					// we are definately done
					if(cChar==0)
						break;

					// the first byte for numbers should be #
					if(iChars==1 && cChar=='#')
					{
						iFrom++;
						bCharCode=true;
						continue;
					}

					if(bCharCode && iChars==2 && cChar=='x')
					{
						iFrom++;
						iEntLen--;
						bCharCodeHex=true;
					}

					//Console.WriteLine("Got entity end: {0}",sEntity);
					// Break on:
					// 1) ; - proper end of entity
					// 2) number 10-based entity but current byte is not a number
					if(cChar==';' || (bCharCode && !bCharCodeHex && !char.IsNumber((char)cChar)))
					{

#if UNSAFE_CODE
						fixed(byte* pBuffer=bHTML)
						{
							sEntity=new String((sbyte*)pBuffer,iFrom,iEntLen,System.Text.Encoding.Default);
#else
					{
						sEntity=System.Text.Encoding.Default.GetString(bHTML,iFrom,iEntLen);
#endif

							if(bCharCode)
							{
								// NOTE: this may fail due to wrong data format,
								// in which case we will return 0, and entity will be
								// ignored
								if(iEntLen>0)
								{
									int iChar=0;
									
									if(!bCharCodeHex)
										iChar=int.Parse(sEntity);
									else
										iChar=int.Parse(sEntity,System.Globalization.NumberStyles.HexNumber);
								
									return (char)iChar;
								}
							}
								
							if(iEntLen>=iMinEntityLen && iEntLen<=iMaxEntityLen && oEntities.Contains(sEntity))
							{
								return (char)((int)oEntities[sEntity]);
							}
						}

						break;
					}

					// as soon as entity length exceed max length of entity known to us
					// we break up the loop and return nothing found

					if(iEntLen>iMaxEntityLen)
						break;

					//sEntity+=(char)cChar;
					iEntLen++;
				}
			}
			catch(Exception oEx)
			{
				Console.WriteLine("Entity parsing exception: "+oEx.ToString());
			}

			// if we have not found squat, then we will need to put point back
			// to where it was before this function was called
			if(iChars>0)
				PutChars(iChars);

			return (char)(0);
		}

		/// <summary>
		/// Loads HTML from file
		/// </summary>
		/// <param name="sFileName">Full filename</param>
		public void LoadFromFile(string sFileName)
		{
			CleanUp();

			StreamReader oSR;
			
			oSR=File.OpenText(sFileName);
			string oHTML=oSR.ReadToEnd();
			oSR.Close();

			Init(oHTML);

			return;
		}
	
	}

}

// THE END
