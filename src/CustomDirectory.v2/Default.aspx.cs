using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using System.Xml;

using System.Net;
using System.IO;
using CustomDirectory.v2.Model;

namespace CustomDirectory.v2
{
    public partial class _Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string first = Request.QueryString["f"];
            string last = Request.QueryString["l"];
            string country = Request.QueryString["p"];
            string number = Request.QueryString["n"];
            string start = Request.QueryString["start"];
            string half = Request.QueryString["half"];

            if (first == null) first = string.Empty;
            if (last == null) last = string.Empty;
            if (country == null) country = string.Empty;
            if (number == null) number = string.Empty;
            if (start == null) start = string.Empty;
            if (half == null) half = string.Empty;

            var directories = GetDirectories(first, last, number, start, country);

            Response.ContentType = "text/xml";
            //Response.Write(finalXML);
        }


        private string GetDirectory(string pais, string last, string first, string number, string start)
        {
            string url =GetDirectoryUrlByCountry(pais) + "?l=" + last + "&f=" + first + "&n=" + number + "&start=" + start;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            string cadena = sr.ReadToEnd();
            sr.Close();

            cadena = FixFormatDirectoryString(cadena, pais);
            cadena = DeleteBottomMenu(cadena);
            
            
            
            //cadena = SelectFirstNRecords(cadena, 16);
            
            return cadena;
        }
        private string GetDirectoryUrlByCountry(string country)
        {
            if (string.Equals(country, "argentina", StringComparison.InvariantCultureIgnoreCase))
                return System.Configuration.ConfigurationManager.AppSettings.Get("DirectoryUrlArgentina");
            else
                return System.Configuration.ConfigurationManager.AppSettings.Get("DirectoryUrlChile");
        }
        private string GetPrefixByCountry(string country)
        {
            if (string.Equals(country, "argentina", StringComparison.InvariantCultureIgnoreCase))
                return System.Configuration.ConfigurationManager.AppSettings.Get("Prefix_Argentina");
            else
                return System.Configuration.ConfigurationManager.AppSettings.Get("Prefix_Chile");
        }
        private string BuildFinalXML(string FullDirectory, string countryMessage)
        {
            return "<CiscoIPPhoneDirectory>" + Environment.NewLine + FullDirectory +
                    "<Prompt>" + countryMessage + "</Prompt>" + Environment.NewLine +
                    "<SoftKeyItem>" + Environment.NewLine +
                        "<Name>Llamar</Name>" + Environment.NewLine +
                        "<URL>SoftKey:Dial</URL>" + Environment.NewLine +
                        "<Position>1</Position>" + Environment.NewLine +
                    "</SoftKeyItem>" + Environment.NewLine +
                    "<SoftKeyItem>" + Environment.NewLine +
                        "<Name>Editar</Name>" + Environment.NewLine +
                        "<URL>SoftKey:EditDial</URL>" + Environment.NewLine +
                        "<Position>2</Position>" + Environment.NewLine +
                    "</SoftKeyItem>" + Environment.NewLine +
                    "<SoftKeyItem>" + Environment.NewLine +
                        "<Name>Salir</Name>" + Environment.NewLine +
                        "<URL>SoftKey:Exit</URL>" +
                        "<Position>3</Position>" + Environment.NewLine +
                    "</SoftKeyItem>" + Environment.NewLine +


                "<SoftKeyItem>" + Environment.NewLine +
                "<Name>Next</Name>" + Environment.NewLine +
                "<URL>" + System.Configuration.ConfigurationManager.AppSettings.Get("SelfUrl") + "?start=32</URL>" + Environment.NewLine +
                "<Position>4</Position>" + Environment.NewLine +
                "</SoftKeyItem>" + Environment.NewLine +

                "<SoftKeyItem>" + Environment.NewLine +
                "<Name>Search</Name>" + Environment.NewLine +
                "<URL>" + System.Configuration.ConfigurationManager.AppSettings.Get("SelfUrl") + "</URL>" + Environment.NewLine +
                "<Position>5</Position>" + Environment.NewLine +
                "</SoftKeyItem>" + Environment.NewLine +
                "</CiscoIPPhoneDirectory>";
        }
        private string DeleteBottomMenu(string cadena)
        {
            for (int i = 0; i < cadena.Length; i++)
                if (cadena[i].ToString() == "<" && cadena[i + 1].ToString() == "P")
                    cadena = cadena.Substring(0, i);
            return cadena;
        }
        private string SelectFirstNRecords(string cadena, int recordsCount)
        {
            int cant = 0;
            for (int i = 0; i < cadena.Length; i++)
            {
                if (cadena[i].ToString() == "<" && cadena[i + 1].ToString() == "D")
                    cant++;
                if (cant == recordsCount)
                    cadena = cadena.Substring(0, i);
            }
            return cadena;
        }
        private string FixFormatDirectoryString(string cadena, string pais)
        {
            string prefix = GetPrefixByCountry(pais);
            cadena = cadena.Replace("<?xml version=\"1.0\"?>", "").
                            Replace("<CiscoIPPhoneDirectory>", "").
                            Replace("<Name>", "<Name>[ARG] ").
                            Replace("Garc�a", "Garcia").
                            Replace("<Telephone>", "<Telephone>" + prefix);

            return cadena;
        }
        private string ConcatDirectories(List<string> directories)
        {
            var directoryFull = string.Empty;
            foreach (var item in directories)
            {
                directoryFull += Environment.NewLine + item;
            }
            return directoryFull;
        }
        private List<IPDirectory> GetDirectories(string first, string last, string number, string start, string country)
        {
            var ClDirectory = string.Empty;
            var ArgDirectory = string.Empty;
            var FullDirectory = string.Empty;
            var countryMessage = string.Empty;
            var notRecordsFound = false;
            var finalXML = string.Empty;

            if (country == string.Empty)
            {
                ClDirectory = GetDirectory("chile", last, first, number, start.ToString());
                ArgDirectory = GetDirectory("argentina", last, first, number, start.ToString());

                var directories = new List<string>();
                directories.Add(ClDirectory);
                directories.Add(ArgDirectory);
                
                FullDirectory = ConcatDirectories(directories);
                countryMessage = "Records from all countries";
            }
            else if (string.Equals(country, "cl", StringComparison.InvariantCultureIgnoreCase))
            {
                ClDirectory = GetDirectory("chile", last, first, number, start.ToString());
                countryMessage = "Records from Chile";
            }
            else if (string.Equals(country, "arg", StringComparison.InvariantCultureIgnoreCase))
            {
                ArgDirectory = GetDirectory("argentina", last, first, number, start.ToString());
                countryMessage = "Records from Argentina";
            }
            else
            {
                notRecordsFound = true;
            }

            if (!notRecordsFound)
            {
                finalXML = BuildFinalXML(FullDirectory, countryMessage);
            }
            else
            {
                finalXML = "<CiscoIPPhoneDirectory><Prompt>Busqueda sin coincidencias</Prompt></CiscoIPPhoneDirectory>";
            }
            return null;
        }
    }
}