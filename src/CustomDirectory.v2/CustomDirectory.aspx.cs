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
    public partial class _CustomDirecotory : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
            }
            var xmlOutput = string.Empty;
            string first = Request.QueryString["f"];
            string last = Request.QueryString["l"];
            string country = Request.QueryString["p"];
            string number = Request.QueryString["n"];
            string start = Request.QueryString["start"];
            string page = Request.QueryString["page"];

            if (first == null) first = string.Empty;
            if (last == null) last = string.Empty;
            if (country == null) country = string.Empty;
            if (number == null) number = string.Empty;
            if (start == null) start = string.Empty;
            if (page == null) page = "1";

            var directories = new List<IPPhoneDirectory>();
            var countryValidado = string.Empty;
            if (country != string.Empty)
            {
                countryValidado = GetCountryNameByCode(country);
                if (countryValidado == string.Empty)
                    xmlOutput = "Invalid Country";
            }
            directories = GetDirectories(first, last, number, countryValidado, start);
            var directoryListOrdered = new List<IPPhoneDirectoryEntry>();
            foreach (var dir in directories)
	        {
                var entriesWithCountryCode = AddCountryCodeToDirectoryEntries(dir);
                directoryListOrdered.AddRange(entriesWithCountryCode.DirectoryEntries);
	        }


            if (Int32.Parse(page) + 31 > directoryListOrdered.Count)
            {
                //obtener mas registros
                directories = GetDirectories(first, last, number, countryValidado, (Int32.Parse(start) + 31).ToString());
                foreach (var dir in directories)
                {
                    var entriesWithCountryCode = AddCountryCodeToDirectoryEntries(dir);
                    directoryListOrdered.AddRange(entriesWithCountryCode.DirectoryEntries);
                }
            }

            var selection = GetEntriesByPage(directoryListOrdered, page);
            var stringXMLOrderedEntries = CovertEntriesToString(selection);

            xmlOutput = "<?xml version=\"1.0\"?>" + Environment.NewLine +
                        "<CiscoIPPhoneDirectory>" + Environment.NewLine +
                        stringXMLOrderedEntries +   Environment.NewLine +
                        "</CiscoIPPhoneDirectory>";
                        

            Response.ContentType = "text/xml";
            Response.Write(xmlOutput);
        }

        private List<IPPhoneDirectoryEntry> GetEntriesByPage(List<IPPhoneDirectoryEntry> listEntries, string page)
        {
            var list = new List<IPPhoneDirectoryEntry>();
            var intPage = Int32.Parse(page);
            for (int i = intPage - 1; i < intPage + 31; i++)
                list.Add(listEntries[i]);

            return list;
        }
        private IPPhoneDirectory AddCountryCodeToDirectoryEntries(IPPhoneDirectory dir)
        {
            var dirWithCountryCode = new IPPhoneDirectory();
            dirWithCountryCode.Country = dir.Country;
            dirWithCountryCode.EntriesCount = dir.EntriesCount;
            dirWithCountryCode.Prefix = dir.Prefix;
            dirWithCountryCode.DirectoryEntries = new List<IPPhoneDirectoryEntry>();
            var countryCode = "[" + GetCountryCodeByName(dir.Country).ToUpper() + "] ";

            foreach (var item in dir.DirectoryEntries)
            {
                var entry = new IPPhoneDirectoryEntry();
                entry.Name =  countryCode + item.Name;
                entry.Telephone = item.Telephone;
                dirWithCountryCode.DirectoryEntries.Add(entry);
            }
            return dirWithCountryCode;
        }
        private string CovertEntriesToString(List<IPPhoneDirectoryEntry> list)
        {
            string xmlDirectories = string.Empty;
            //list.Sort(list;
            foreach (var item in list)
            {
                xmlDirectories += "<DirectoryEntry>" + Environment.NewLine +
                                  "<Name>" + item.Name + "</Name>" + Environment.NewLine +
                                  "<Telephone>" + item.Telephone + "</Telephone>" + Environment.NewLine +
                                  "</DirectoryEntry>" + Environment.NewLine;
            }
            return xmlDirectories;
        }        
        private string GetStringDirectory(string first, string last, string number, string country, string start)
        {
            var url = GetDirectoryUrlByCountry(country) + "?l=" + last + "&f=" + first + "&n=" + number + "&start=" + start;
            var request = (HttpWebRequest)WebRequest.Create(url);
            var response = (HttpWebResponse)request.GetResponse();
            var sr = new StreamReader(response.GetResponseStream());
            var stringDirectory = sr.ReadToEnd();
            sr.Close();

            stringDirectory = FixFormatDirectoryString(stringDirectory, country);
            stringDirectory = DeleteBottomMenu(stringDirectory);

            stringDirectory = stringDirectory.Replace("<DirectoryEntry>", "#");
            stringDirectory = stringDirectory.Replace("</DirectoryEntry>", "");

            return stringDirectory;
        }
        private string GetDirectoryUrlByCountry(string country)
        {
            return System.Configuration.ConfigurationManager.AppSettings.Get("DirectoryUrl_" + country);
        }
        private string GetPrefixByCountry(string country)
        {
            return System.Configuration.ConfigurationManager.AppSettings.Get("Prefix_" + country);
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
                "<URL>" + System.Configuration.ConfigurationManager.AppSettings.Get("localhost") + "?start=32</URL>" + Environment.NewLine +
                "<Position>4</Position>" + Environment.NewLine +
                "</SoftKeyItem>" + Environment.NewLine +

                "<SoftKeyItem>" + Environment.NewLine +
                "<Name>Search</Name>" + Environment.NewLine +
                "<URL>" + System.Configuration.ConfigurationManager.AppSettings.Get("localhost") + "</URL>" + Environment.NewLine +
                "<Position>5</Position>" + Environment.NewLine +
                "</SoftKeyItem>" + Environment.NewLine +
                "</CiscoIPPhoneDirectory>";
        }
        private string DeleteBottomMenu(string stringDirectory)
        {
            for (int i = 0; i < stringDirectory.Length; i++)
                if (stringDirectory[i].ToString() == "<" && stringDirectory[i + 1].ToString() == "P")
                    stringDirectory = stringDirectory.Substring(0, i);
            return stringDirectory;
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
        private string FixFormatDirectoryString(string stringDirectory, string country)
        {
            stringDirectory = stringDirectory.Replace("<?xml version=\"1.0\"?>", "").
                              Replace("<CiscoIPPhoneDirectory>", "").
                              Replace("Garc�a", "Garcia");

            return stringDirectory;
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
        private List<IPPhoneDirectory> GetDirectories(string first, string last, string number, string country, string start)
        {
            var IPPhoneDirectories = new List<IPPhoneDirectory>();
            var countries = GetAvailableCountries();
            if (country == string.Empty)
            {
                foreach (var countryItem in countries)
                    IPPhoneDirectories.Add(GetSingleDirectory(first, last, number, countryItem.Value, start));
            }
            else
            {
                IPPhoneDirectories.Add(GetSingleDirectory(first, last, number, country, start));
            }

            return IPPhoneDirectories;
            //if(country == "")

            //    ClDirectory = GetDirectory("chile", last, first, number, start.ToString());
            //    ArgDirectory = GetDirectory("argentina", last, first, number, start.ToString());

            //    var directories = new List<string>();
            //    directories.Add(ClDirectory);
            //    directories.Add(ArgDirectory);

            //    FullDirectory = ConcatDirectories(directories);
            //    countryMessage = "Records from all countries";
            //}
            //else if (string.Equals(country, "cl", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    ClDirectory = GetDirectory("chile", last, first, number, start.ToString());
            //    countryMessage = "Records from Chile";
            //}
            //else if (string.Equals(country, "arg", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    ArgDirectory = GetDirectory("argentina", last, first, number, start.ToString());
            //    countryMessage = "Records from Argentina";
            //}
            //else
            //{
            //    notRecordsFound = true;
            //}

            //if (!notRecordsFound)
            //{
            //    finalXML = BuildFinalXML(FullDirectory, countryMessage);
            //}
            //else
            //{
            //    finalXML = "<CiscoIPPhoneDirectory><Prompt>Busqueda sin coincidencias</Prompt></CiscoIPPhoneDirectory>";
            //}
        }
        private Dictionary<string, string> GetAvailableCountries()
        {
            var listCountries = new Dictionary<string, string>();
            var arrCountries = System.Configuration.ConfigurationManager.AppSettings.Get("Countries").Split('|');
            for (int i = 0; i < arrCountries.Count(); i++)
            {
                listCountries.Add(arrCountries[i].Split(':')[1], arrCountries[i].Split(':')[0]);
            }
            return listCountries;
        }
        private int GetDirectoryEntriesCount(string first, string last, string number, string country)
        {
            string url = GetDirectoryUrlByCountry(country) + "?l=" + last + "&f=" + first + "&n=" + number;// +"&start=" + start;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream());
            var stringDirectory = sr.ReadToEnd();
            sr.Close();

            var index = stringDirectory.IndexOf("</Prompt>");
            while (stringDirectory[index] != ' ')
                index--;

            var recordsCount = string.Empty;
            while (stringDirectory[index] != '<')
            {
                recordsCount += stringDirectory[index];
                index++;
            }

            //index = stringDirectory.IndexOf(";start=");
            //var startParameter = string.Empty;
            //while (stringDirectory[index] != '<')
            //{
            //    startParameter += stringDirectory[index];
            //    index++;
            //}
            int aux = 0;
            int.TryParse(recordsCount, out aux);
            return aux;
        }
        private List<IPPhoneDirectoryEntry> GetEntriesList(string first, string last, string number, string country, string start)
        {
            var list = new List<IPPhoneDirectoryEntry>();
            var stringEntries = GetStringDirectory(first, last, number, country, start);
            var arrayEntries = stringEntries.Split('#');
            foreach (var entry in arrayEntries)
            {
                if (entry.Contains("<Name>"))
                {
                    var entryFixed = entry.Replace("<Name>", string.Empty)
                                          .Replace("</Name>", "#")
                                          .Replace("</Telephone>", string.Empty)
                                          .Replace("<Telephone>", string.Empty);

                    var arrayEntry = entryFixed.Split('#');

                    var IPEntry = new IPPhoneDirectoryEntry();

                    IPEntry.Name = arrayEntry[0].Replace("\r\n", string.Empty).TrimStart();
                    IPEntry.Telephone = arrayEntry[1].Replace(" ", string.Empty).Replace("\r\n", string.Empty);
                    list.Add(IPEntry);
                }
            }
            return list;
        }
        private IPPhoneDirectory GetSingleDirectory(string first, string last, string number, string country, string start)
        {
            var Directory = new IPPhoneDirectory();
            var entriesList = new List<IPPhoneDirectoryEntry>();

            Directory.Country = country;
            Directory.EntriesCount = GetDirectoryEntriesCount(first, last, number, Directory.Country);
            Directory.Prefix = GetPrefixByCountry(Directory.Country);

            entriesList.AddRange(GetEntriesList(first, last, number, Directory.Country, start));
            Directory.DirectoryEntries = entriesList;

            return Directory;
        }
        private string GetCountryNameByCode(string countryCode)
        {
            var countries = GetAvailableCountries();
            foreach (var countryItem in countries)
            {
                if (countryCode == countryItem.Key)
                    return countryItem.Value; ;
            }
            return string.Empty;
        }
        private string GetCountryCodeByName(string countryName)
        {
            var countries = GetAvailableCountries();
            foreach (var countryItem in countries)
            {
                if (countryName == countryItem.Value)
                    return countryItem.Key;
            }
            return string.Empty;
        }
    }
}