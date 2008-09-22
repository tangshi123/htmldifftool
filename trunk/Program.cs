using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace HtmlDiff
{
    class Program
    {
        static void Main(string[] args)
        {/*
            if (args.Length != 2)
            {
                Console.WriteLine("Must be in format htmldiff <file1> <file2> (found " + args.Length + " arguments)");

                return;
            }
            */
            
            string file1 = @"C:\Documents and Settings\work\My Documents\a.html"; // args[0] as string
            string file2 = @"C:\Documents and Settings\work\My Documents\b.html"; // args[1] as string

            string html1 = File.ReadAllText(file1);
            string html2 = File.ReadAllText(file2);

            HtmlDiff diff = new HtmlDiff(html1, html2);



            string openHtmlA = string.Format(@"<html><header><title>HtmlA of <u>{0}</u> and <u>{1}</u></title></header><body><span style=""font-family:Arial; "">HtmlA of <u>{0}</u> and <u>{1}</u><br>[HtmlA] [<a href=HtmlB.html>HtmlB</a>] [<a href=SourceA.html>SourceA</a>] [<a href=SourceB.html>SourceB</a>]</span><hr>", file1, file2);
            string openHtmlB = string.Format(@"<html><header><title>HtmlB of <u>{0}</u> and <u>{1}</u></title></header><body><span style=""font-family:Arial; "">HtmlB of <u>{0}</u> and <u>{1}</u><br>[<a href=HtmlA.html>HtmlA</a>] [HtmlB] [<a href=SourceA.html>SourceA</a>] [<a href=SourceB.html>SourceB</a>]</span><hr>", file1, file2);
            string openSourceA = string.Format(@"<html><header><title>SourceA of <u>{0}</u> and <u>{1}</u></title></header><body><span style=""font-family:Arial; "">SourceA of <u>{0}</u> and <u>{1}</u><br>[<a href=HtmlA.html>HtmlA</a>] [<a href=HtmlB.html>HtmlB</a>] [SourceA] [<a href=SourceB.html>SourceB</a>]</span><hr>", file1, file2);
            string openSourceB = string.Format(@"<html><header><title>SourceB of <u>{0}</u> and <u>{1}</u></title></header><body><span style=""font-family:Arial; "">SourceB of <u>{0}</u> and <u>{1}</u><br>[<a href=HtmlA.html>HtmlA</a>] [<a href=HtmlB.html>HtmlB</a>] [<a href=SourceA.html>SourceA</a>] [SourceB]</span><hr>", file1, file2);

            string close = "</body></html>";


            Console.WriteLine("Saved HtmlA.html");
            File.WriteAllText("HtmlA.html", openHtmlA + diff.DiffedHtml1 + close);

            Console.WriteLine("Saved HtmlA.html");
            File.WriteAllText("HtmlB.html", openHtmlB + diff.DiffedHtml2 + close);

            Console.WriteLine("Saved HtmlA.html");
            File.WriteAllText("SourceA.html", openSourceA + diff.DiffedSource1 + close);

            Console.WriteLine("Saved HtmlA.html");
            File.WriteAllText("SourceB.html", openSourceB + diff.DiffedSource2 + close);

        }
    }
}
