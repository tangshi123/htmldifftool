using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Text.RegularExpressions;
using Majestic12;
using Menees.DiffUtils;

namespace HtmlDiff
{
    /// <summary>
    /// Summary description for HtmlDiff
    /// </summary>
    public class HtmlDiff
    {
        
        private string _html1;
        private string _html2;

        private bool _areDifferent;

        private string _diffedSource1 = "";
        private string _diffedSource2 = "";
        private string _diffedHtml1 = "";
        private string _diffedHtml2 = "";
        private string _addedKeywords = "";

        public string Html1 { get { return _html1; } }
        public string Html2 { get { return _html2; } }

        public bool AreDifferent { get { return _areDifferent; } }

        public string DiffedSource1 { get { return _diffedSource1; } }
        public string DiffedSource2 { get { return _diffedSource2; } }
        public string DiffedHtml1 { get { return _diffedHtml1; } }
        public string DiffedHtml2 { get { return _diffedHtml2; } }




        public HtmlDiff(string html1, string html2)
        {
            _html1 = html1;
            _html2 = html2;

            diff();
        }



        static class Tags
        {
            public static string delete = "<div style='background-color:#FF7070; display: inline; position: static'>";
            public static string add = "<div style='background-color:#77FF7C; display: inline; position: static'>";
            public static string changeDelete = "<div style='background-color:#FFC45F; display: inline; position: static'>";
            public static string changeAdd = "<div style='background-color:#FCFF5B; display: inline; position: static'>";
            public static string close = "</div>";
        }


        private static int[] AsciiToIntArray(string ascii)
        {
            System.Text.ASCIIEncoding encoder = new System.Text.ASCIIEncoding();

            byte[] bytes = encoder.GetBytes(ascii.ToCharArray());
            int[] intArr = new int[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
                intArr[i] = System.Convert.ToInt32(bytes[i]);

            return intArr;
        }

        /// <summary>
        /// Breaks up the string by newline
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string[] toStringList(string str)
        {
            string[] arr = str.Split('\n');
            return arr;
        }

        private HTMLchunk[] htmlParse(string str)
        {
            //return value
            ArrayList ret = new ArrayList();

            //init parser
            Majestic12.HTMLparser parser = new Majestic12.HTMLparser();

            //keep raw html because we need to reconstruct it
            parser.bKeepRawHTML = true;
            //keep text... this is for parsing just tags
            parser.bTextMode = true;
            //initialize to parse the string
            parser.Init(str);

            Majestic12.HTMLchunk chunk = null;
            // we parse until returned chunk is null indicating we reached end of parsing
            while ((chunk = parser.ParseNext()) != null)
            {

                //discard empty blocks for performance increase
                if (chunk.oHTML.Trim() != "")
                {
                    //hard copy the chunk
                    HTMLchunk clone = new HTMLchunk(false);
                    clone.oHTML = String.Copy(chunk.oHTML);
                    clone.oType = chunk.oType;
                    clone.sTag = String.Copy(chunk.sTag);

                    ret.Add(clone);
                }
            }

            parser.CleanUp();

            //return string array
            return (HTMLchunk[])ret.ToArray(typeof(HTMLchunk));
        }




        private int[] hash(HTMLchunk[] chunks)
        {
            //return value
            int[] hash = new int[chunks.Length];

            //hash the chunks
            Menees.DiffUtils.StringHasher hasher = new Menees.DiffUtils.StringHasher(Menees.DiffUtils.HashType.CRC32, true, true, 0);
            for (int i = 0; i < chunks.Length; i++)
                hash[i] = hasher.GetHashCode(chunks[i].oHTML);

            return hash;

        }

        /// <summary>
        /// Diffs two html strings and marks them up
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private void diff()
        {
            //parse the html
            HTMLchunk[] chunks1 = htmlParse(_html1);
            HTMLchunk[] chunks2 = htmlParse(_html2);

            //make a hash array of the chunks
            int[] hash1 = hash(chunks1);
            int[] hash2 = hash(chunks2);

            //diff the hashes
            Menees.DiffUtils.MyersDiff myerdiff = new Menees.DiffUtils.MyersDiff(hash1, hash2);
            //get a collection of changes
            EditScript edits = myerdiff.Execute();

            //record if there are any differences
            _areDifferent = (edits.Count > 0);

            //markup changes
            _diffedSource1 = getMarkedUpSource(chunks1, edits, true);
            _diffedSource2 = getMarkedUpSource(chunks2, edits, false);
            _diffedHtml1 = getMarkedUpHtml(chunks1, edits, true);
            _diffedHtml2 = getMarkedUpHtml(chunks2, edits, false);
            _addedKeywords = getKewords(chunks2, edits);
        }



        private static string getMarkedUpSource(HTMLchunk[] chunks, Menees.DiffUtils.EditScript edits, bool isOlderVersion)
        {
            string[] str = new string[chunks.Length];
            //html encode the source so it wont render
            for (int i = 0; i < str.Length; i++) str[i] = System.Web.HttpUtility.HtmlEncode(chunks[i].oHTML);

            //get an iterator for the changes          
            System.Collections.IEnumerator it = edits.GetEnumerator();

            while (it.MoveNext())
            {
                Menees.DiffUtils.Edit curr = (Menees.DiffUtils.Edit)it.Current;
                int start = (isOlderVersion ? curr.StartA : curr.StartB);
                switch (curr.Type)
                {
                    case Menees.DiffUtils.EditType.Change:
                        //changes are marked as deletes in older version and adds in newer version
                        str[start] = (isOlderVersion ? Tags.changeDelete : Tags.changeAdd) + str[start];
                        str[start + curr.Length] += Tags.close;
                        break;

                    case Menees.DiffUtils.EditType.Delete:
                        //deletes are marked in the older version
                        if (isOlderVersion)
                        {
                            str[start] = Tags.delete + str[start];
                            str[start + curr.Length] += Tags.close;
                        }
                        break;

                    case Menees.DiffUtils.EditType.Insert:
                        //Inserts are marked in the newer version
                        if (!isOlderVersion)
                        {
                            str[start] = Tags.add + str[start];
                            str[start + curr.Length] += Tags.close;
                        }
                        break;
                }
            }

            return String.Join("", str);
        }

        private static string getKewords(HTMLchunk[] chunks, Menees.DiffUtils.EditScript edits)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            //get an iterator for the changes          
            System.Collections.IEnumerator it = edits.GetEnumerator();
            while (it.MoveNext())
            {

                Menees.DiffUtils.Edit curr = (Menees.DiffUtils.Edit)it.Current;
                //append only new text additions to versionB
                if (curr.Type == EditType.Insert || curr.Type == EditType.Change)
                    for (int i = 0; i < curr.Length; i++)
                        //append only text changes
                        if (chunks[curr.StartB + i].oType == HTMLchunkType.Text)
                            sb.Append(" " + chunks[curr.StartB + i].oHTML);
            }

            return sb.ToString();
        }

        private static string getMarkedUpHtml(HTMLchunk[] chunks, Menees.DiffUtils.EditScript edits, bool isOlderVersion)
        {
            string[] str = new string[chunks.Length];
            for (int i = 0; i < str.Length; i++) str[i] = chunks[i].oHTML;

            //get an iterator for the changes          
            System.Collections.IEnumerator it = edits.GetEnumerator();

            //for now only mark up text nodes!!! this needs improvement
            while (it.MoveNext())
            {
                Menees.DiffUtils.Edit curr = (Menees.DiffUtils.Edit)it.Current;
                int start = (isOlderVersion ? curr.StartA : curr.StartB);
                switch (curr.Type)
                {
                    case Menees.DiffUtils.EditType.Change:
                        for (int i = 0; i < curr.Length; i++)
                            if (chunks[start + i].oType == HTMLchunkType.Text)
                                str[start + i] = (isOlderVersion ? Tags.changeDelete : Tags.changeAdd) + str[start + i] + Tags.close;
                        break;

                    case Menees.DiffUtils.EditType.Delete:
                        //deletes are marked in the older version
                        if (isOlderVersion)
                            for (int i = 0; i < curr.Length; i++)
                                if (chunks[start + i].oType == HTMLchunkType.Text)
                                    str[start + i] = Tags.delete + str[start + i] + Tags.close;
                        break;

                    case Menees.DiffUtils.EditType.Insert:
                        //Inserts are marked in the newer version
                        if (!isOlderVersion)
                            for (int i = 0; i < curr.Length; i++)
                                if (chunks[start + i].oType == HTMLchunkType.Text)
                                    str[start + i] = Tags.add + str[start + i] + Tags.close;
                        break;
                }
            }

            return String.Join("", str);
        }
    }
}