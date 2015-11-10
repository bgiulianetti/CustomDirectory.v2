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
using System.Net.Http;
using System.Net.Http.Headers;

namespace CustomDirectory.v2
{
    public partial class _CustomDirecotory : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            #region QueryStrings
            var xmlOutput = string.Empty;
            var first = "ale";// Request.QueryString["f"];
            var last = Request.QueryString["l"];
            var countryCode = Request.QueryString["p"];
            var number = Request.QueryString["n"];
            var start = Request.QueryString["start"];
            var page = Request.QueryString["page"];

            if (first == null) first = string.Empty;
            if (last == null) last = string.Empty;
            if (countryCode == null) countryCode = string.Empty;
            if (number == null) number = string.Empty;
            if (start == null) start = "1";
            if (page == null) page = "1";
            #endregion

            if (countryCode != string.Empty)
            {
                var countryName = GetCountryNameByCode(countryCode);
                if (countryName == string.Empty)
                {
                    xmlOutput = "<Text>No Match: '" + countryCode + "' is not a valid contry code</Text>";
                }
                else
                {
                    var stringSinglePageDirectory = GetStringSinglePageDirectory(new HttpClient(), first, last, number, countryName, start);
                    if (stringSinglePageDirectory == null)
                    {
                        xmlOutput = "<Text>Internal Server Error</Text>";
                    }
                    else
                    {
                        xmlOutput = FixFormatForSingleCountry(stringSinglePageDirectory, countryCode, countryName, first, last, number, start);
                        xmlOutput = FixAccentuation(xmlOutput);
                    }
                }
            }
            else
            {
                var directories = GetAllDirectories(first, last, number, start);
                var allEntries = GetEntriesOrderedAndWithPrefix(directories);
                var selectedEntries = SelectEntriesByPage(allEntries, Int32.Parse(page));
                xmlOutput = BuildXML(selectedEntries, first, last, number, page, allEntries.Count);
                xmlOutput = FixAccentuation(xmlOutput);
            }
            Response.ContentType = "text/xml";
            Response.Write(xmlOutput);
        }


        /// <summary>
        /// Gets 31 entries form a directory, counting from the 'start' parameter.
        /// </summary>
        /// <param name="client">Http Client instance (You cannot get the second page without requesting the first one)</param>
        /// <param name="first">First name</param>
        /// <param name="last">Last name</param>
        /// <param name="number">Phone number</param>
        /// <param name="country">Country name</param>
        /// <param name="start">The number of records to skip</param>
        /// <returns></returns>
        private string GetStringSinglePageDirectory(HttpClient client, string first, string last, string number, string countryName, string start)
        {
            var directoryUrl = GetDirectoryUrlByCountry(countryName);
            if (directoryUrl == null)
                return null;
            client.BaseAddress = new Uri(directoryUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = null;
            var intStart = Int32.Parse(start);
            for (int i = 1; i <= intStart; i += 31)
            {
                var request = "?l=" + last + "&f=" + first + "&n=" + number + "&start=" + i.ToString();
                try
                {
                    response = client.GetAsync(request).Result;
                }
                catch
                {
                    return null;
                }
                if (!response.IsSuccessStatusCode)
                    return null;
            }
            return response.Content.ReadAsStringAsync().Result;
        }
        
        /// <summary>
        /// Gets From the Web.Config all available countries with its names, codes, and prefies
        /// </summary>
        /// <returns>List<Country></returns>
        private List<Country> GetAvailableCountries()
        {
            List<Country> countryList = new List<Country>();
            var arrCountries = System.Configuration.ConfigurationManager.AppSettings.Get("Countries").Split('|');
            for (int i = 0; i < arrCountries.Count(); i++)
            {
                var country = new Country(name: arrCountries[i].Split(':')[0],
                                          code: arrCountries[i].Split(':')[1],
                                          prefix: System.Configuration.ConfigurationManager.AppSettings.Get("Prefix_" + arrCountries[i].Split(':')[0]));
                countryList.Add(country);
            }
            return countryList;
        }

        /// <summary>
        /// Gets the name of a country by it´s code
        /// </summary>
        /// <param name="countryCode"></param>
        /// <returns></returns>
        private string GetCountryNameByCode(string countryCode)
        {
            var countries = GetAvailableCountries();
            foreach (var countryItem in countries)
            {
                if (countryCode == countryItem.Code)
                    return countryItem.Name; ;
            }
            return string.Empty;
        }
               
        /// <summary>
        /// Builds a string format to add a Directory URL as a QueryString to perform a search
        /// </summary>
        /// <param name="first">Name of a contact</param>
        /// <param name="last">Last name of a contact</param>
        /// <param name="number">Phone number of a contact</param>
        /// <param name="start">Number of entries to skip</param>
        /// <returns>Query String search</returns>
        private string BuildQueryStringSearch(string first, string last, string number, string start, string page = null)
        {
            if (page == null)
                return "?l=" + last + "&f=" + first + "&n=" + number + "&start=" + start;
            else
                return "?l=" + last + "&f=" + first + "&n=" + number + "&page=" + page;
        }

        /// <summary>
        /// Fixes the string format, wrong accentuation, adds country code to each record.
        /// </summary>
        /// <param name="stringDirectory"></param>
        /// <param name="countryCode"></param>
        /// <param name="countryName"></param>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="number"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        private string FixFormatForSingleCountry(string stringDirectory, string countryCode, string countryName, string first, string last, string number, string start)
        {
            return stringDirectory.Replace("<Name>", "<Name>[" + countryCode.ToUpper() + "] ")
                                  //.Replace("<Prompt>Records", "<Prompt>Contactos")
                                  //.Replace(" to ", " a ")
                                  //.Replace(" of ", " de ")
                                  .Replace("<Name>[" + countryCode.ToUpper() + "] Dial", "<Name>Dial")
                                  .Replace("<Name>[" + countryCode.ToUpper() + "] Search", "<Name>Search")
                                  .Replace("<Name>[" + countryCode.ToUpper() + "] Exit", "<Name>Exit")
                                  .Replace("<Name>[" + countryCode.ToUpper() + "] EditDial", "<Name>EditDial")
                                  .Replace("<Name>[" + countryCode.ToUpper() + "] Next", "<Name>Next")
                                  .Replace("<URL>" + GetUrlDirectoryByName(countryName) + BuildQueryStringSearch(first, last, number, (Int32.Parse(start) + 31).ToString()).Replace("&", "&amp;") + "</URL>",
                                           "<URL>" + GetUrlCustomDirectory() + BuildQueryStringSearch(first, last, number, (Int32.Parse(start) + 31).ToString()).Replace("&", "&amp;") + "</URL>")
                                  .Replace(("<URL>" + GetUrlDirectoryLandingByName(countryName) + "</URL>").Replace("&f", "&amp;f").Replace("&n", "&amp;n").Replace("&start", "&amp;start"),
                                            "<URL>" + GetUrlDirectoryLandingByName(countryName) + "</URL>")
                                  .Replace("<?xml version=\"1.0\"?>", "");
        }

        /// <summary>
        /// Gets the directory entries count
        /// </summary>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="number"></param>
        /// <param name="country"></param>
        /// <param name="start"></param>
        /// <returns>int</returns>
        private int GetDirectoryEntriesCount(string first, string last, string number, string country, string start)
        {
            var stringDirectory = GetStringSinglePageDirectory(new HttpClient(), first, last, number, country, start);

            var index = stringDirectory.IndexOf("</Prompt>");
            while (stringDirectory[index] != ' ')
                index--;

            var recordsCount = string.Empty;
            while (stringDirectory[index] != '<')
            {
                recordsCount += stringDirectory[index];
                index++;
            }
            int aux = 0;
            int.TryParse(recordsCount, out aux);
            return aux;
        }

        /// <summary>
        /// Deletes all buttons to be printed on screen
        /// </summary>
        /// <param name="stringDirectory"></param>
        /// <returns>string</returns>
        private string DeleteBottomMenu(string stringDirectory)
        {
            for (int i = 0; i < stringDirectory.Length; i++)
                if (stringDirectory[i].ToString() == "<" && stringDirectory[i + 1].ToString() == "P")
                    stringDirectory = stringDirectory.Substring(0, i);
            return stringDirectory;
        }

        /// <summary>
        /// Gets all the entries of a Directory with a specific search criteria
        /// </summary>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="number"></param>
        /// <param name="country"></param>
        /// <param name="start"></param>
        /// <returns>List<IPPhoneDirectoryEntry></returns>
        private List<IPPhoneDirectoryEntry> GetDirectoryEntriesList(string first, string last, string number, string country, string start)
        {
            var list = new List<IPPhoneDirectoryEntry>();

            var isEmpty = false;
            var isFirstTime = true;
            int interruptionLoopTooLong = 0;
            int topEntriesSearch = GetTopEntriesSearch();
            while (!isEmpty)
            {
                if (interruptionLoopTooLong == topEntriesSearch)
                    break;
                if(!isFirstTime)
                {
                    var intStart = Int32.Parse(start) + 31;
                    start = intStart.ToString();
                }
                else
                {
                    isFirstTime = false;
                }
                var directoryPage = GetStringSinglePageDirectory(new HttpClient(), first, last, number, country, start);
                if (!directoryPage.Contains("<Name>Next</Name>"))
                    isEmpty = true;

                directoryPage = DeleteBottomMenu(directoryPage).Replace("<DirectoryEntry>", "#").Replace("</DirectoryEntry>", "");
                var arrayEntries = directoryPage.Split('#');
                foreach (var entry in arrayEntries)
                {
                    if (entry.Contains("<Name>"))
                    {
                        var entryFixed = entry.Replace("<Name>", string.Empty)
                                              .Replace("</Name>", "#")
                                              .Replace("</Telephone>", string.Empty)
                                              .Replace("<Telephone>", string.Empty);

                        var arrayEntry = entryFixed.Split('#');
                        var IpEntry = new IPPhoneDirectoryEntry();
                        IpEntry.Name = arrayEntry[0].Replace("\r\n", string.Empty).TrimStart();
                        IpEntry.Telephone = arrayEntry[1].Replace(" ", string.Empty).Replace("\r\n", string.Empty);
                        list.Add(IpEntry);
                    }
                }
                interruptionLoopTooLong++;
            }
            return list;
        }

        /// <summary>
        /// Gets All entries of each directory available with a specific search criteria
        /// </summary>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="number"></param>
        /// <param name="start"></param>
        /// <returns>List<IPPhoneDirectory></returns>
        private List<IPPhoneDirectory> GetAllDirectories(string first, string last, string number, string start)
        {
            var list = new List<IPPhoneDirectory>();
            var countries = GetAvailableCountries();
            foreach (var itemCountry in countries)
            {
                var IpPhoneDirectory = new IPPhoneDirectory();
                IpPhoneDirectory.Country = itemCountry;
                var listEntries = GetDirectoryEntriesList(first, last, number, itemCountry.Name, start);
                IpPhoneDirectory.DirectoryEntries = listEntries;
                IpPhoneDirectory.EntriesCount = listEntries.Count;
                list.Add(IpPhoneDirectory);
            }
            return list;
        }

        /// <summary>
        /// Gets the directory URL by the country name
        /// </summary>
        /// <param name="country">Name of the country</param>
        /// <returns>string</returns>
        private string GetDirectoryUrlByCountry(string countryName)
        {
            return System.Configuration.ConfigurationManager.AppSettings.Get("UrlDirectory_" + countryName);
        }

        /// <summary>
        /// Gets a List of entries ordered alphabetically and with cpuntry prefix country added from a list of directories
        /// </summary>
        /// <param name="directories">List<IPPhoneDirectory></param>
        /// <returns>List<IPPhoneDirectoryEntry></returns>
        private List<IPPhoneDirectoryEntry> GetEntriesOrderedAndWithPrefix(List<IPPhoneDirectory> directories)
        {
            var listOrderedWithPrefixes = new List<IPPhoneDirectoryEntry>();
            foreach (var dir in directories)
            {
                foreach (var entryItem in dir.DirectoryEntries)
                {
                    var entry = new IPPhoneDirectoryEntry();
                    entry.Name = "[" + dir.Country.Code.ToUpper() + "] " + entryItem.Name;
                    entry.Telephone = dir.Country.Prefix + entryItem.Telephone;
                    listOrderedWithPrefixes.Add(entry);
                }
            }
            //ordernar lista
            return listOrderedWithPrefixes;
        }

        /// <summary>
        /// Returns an XML of a softkey to be printed on screen
        /// </summary>
        /// <param name="name"></param>
        /// <param name="action"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        private string BuildSoftKey(string name, string action, int position)
        {
            return "<SoftKeyItem>" + Environment.NewLine +
                   "<Name>" + name + "</Name>" + Environment.NewLine +
                   "<URL>" + action + "</URL>" + Environment.NewLine +
                   "<Position>" + position.ToString() + "</Position>" + Environment.NewLine +
                   "</SoftKeyItem>" + Environment.NewLine;
        }

        /// <summary>
        /// Gets 31 entries from a entries list start from the parameter page
        /// </summary>
        /// <param name="allEntries"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        private List<IPPhoneDirectoryEntry> SelectEntriesByPage(List<IPPhoneDirectoryEntry> allEntries, int page)
        {
            var selectedEntries = new List<IPPhoneDirectoryEntry>();
            int start = ((page - 1) * 31);
            for (int i = start; i < start + 31; i++)
            {
                if (i == allEntries.Count)
                    break;
                selectedEntries.Add(allEntries[i]);
            }
            return selectedEntries;
        }

        /// <summary>
        /// Builds a string XML formated to be preinted on screen as a final search
        /// </summary>
        /// <param name="entries"></param>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="number"></param>
        /// <param name="page"></param>
        /// <param name="totalEntries"></param>
        /// <returns></returns>
        private string BuildXML(List<IPPhoneDirectoryEntry> entries, string first, string last, string number, string page, int totalEntries)
        {
            var xmlOutput = "<CiscoIPPhoneDirectory>" + Environment.NewLine;
            foreach (var item in entries)
            {
                xmlOutput += ConvertEntryToString(item);
            }

            var intPage = Int32.Parse(page);
            int start = ((intPage - 1) * 31) + 1;

            int entriesPerPage = 0;
            if (totalEntries < 31)
            {
                //Pagina de una busqueda con menos de 31 entradas
                entriesPerPage = totalEntries;
            }
            else
            {
                if (totalEntries - start > 31)
                {   
                    //Pagina de una busqueda de mas de 31 entradas
                    entriesPerPage = start + 31;
                }
                else
                {
                    //Ultima pagina de una busqueda de mas de 31 entradas
                    entriesPerPage = totalEntries;
                }
            }
            xmlOutput += "<Prompt>Registros " + start.ToString() + " a " + (entriesPerPage).ToString() + " de " + totalEntries.ToString() + "</Prompt>" + Environment.NewLine;
            xmlOutput += BuildSoftKey(SoftKey.Dial.ToString(), "SoftKey:" + SoftKey.Dial.ToString(), 1);
            xmlOutput += BuildSoftKey(SoftKey.EditDial.ToString(), "SoftKey:" + SoftKey.EditDial.ToString(), 2);
            xmlOutput += BuildSoftKey(SoftKey.Exit.ToString(), "SoftKey:" + SoftKey.Exit.ToString(), 3);
            xmlOutput += BuildSoftKey(SoftKey.Next.ToString(), GetUrlCustomDirectory() + BuildQueryStringSearch(first, last, number, "",  (intPage + 1).ToString()).Replace("&f", "&amp;f").Replace("&n", "&amp;n").Replace("&start", "&amp;start").Replace("&page=", "&amp;page="), 4);
            xmlOutput += BuildSoftKey(SoftKey.Search.ToString(), GetUrlDirectoryLanding(), 5);
            xmlOutput += "</CiscoIPPhoneDirectory>";

            return xmlOutput;        
        }

        /// <summary>
        /// Returns an string XML formated entry
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private string ConvertEntryToString(IPPhoneDirectoryEntry entry)
        {
            return "<DirectoryEntry>" + Environment.NewLine +
                   "<Name>" + entry.Name + "</Name>" + Environment.NewLine +
                   "<Telephone>" + entry.Telephone + "</Telephone>" + Environment.NewLine + 
                   "</DirectoryEntry>" + Environment.NewLine;
        }

        /// <summary>
        /// Gets the Url Custom directory from the WebConfig
        /// </summary>
        /// <returns></returns>
        private string GetUrlCustomDirectory()
        {
            return System.Configuration.ConfigurationManager.AppSettings.Get("UrlCustomDirectory");
        }

        /// <summary>
        /// Gets the Url directory from the WebConfig by the country name
        /// </summary>
        /// <param name="countryName"></param>
        /// <returns></returns>
        private string GetUrlDirectoryByName(string countryName)
        {
            return System.Configuration.ConfigurationManager.AppSettings.Get("UrlDirectory_" + countryName);
        }

        /// <summary>
        /// Gets the Url directory landing from the WebConfig by the country name
        /// </summary>
        /// <param name="countryName"></param>
        /// <returns></returns>
        private string GetUrlDirectoryLandingByName(string countryName)
        {
            return System.Configuration.ConfigurationManager.AppSettings.Get("UrlDirectory.Landing_" + countryName);
        }

        /// <summary>
        /// Gets the Url directory landing from the WebConfig
        /// </summary>
        /// <param name="countryName"></param>
        /// <returns></returns>
        private string GetUrlDirectoryLanding()
        {
            return System.Configuration.ConfigurationManager.AppSettings.Get("UrlCustomDirectory.Landing");
        }

        /// <summary>
        /// Fixes accentuation and letter 'ñ' on names
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private string FixAccentuation(string text)
        {
            return text.Replace("Garc�a", "García")
                                  .Replace("A�on", "Añon")
                                  .Replace("R�o", "Río")
                                  .Replace("Reuni�n", "Reunión")
                                  .Replace("Fandi�o", "Fandiño")
                                  .Replace("Hern�n", "Hernán")
                                  .Replace("Larra�aga", "Larrañaga")
                                  .Replace("Franc�s", "Francés")
                                  .Replace("Mej�a", "Mejía")
                                  .Replace("M�ximo", "Máximo")
                                  .Replace("Espi�o", "Espiño")
                                  .Replace("Nu�ez", "Nuñez")
                                  .Replace("Iguaz�", "Iguazú")
                                  .Replace("Tr�fico", "Tráfico")
                                  .Replace("Tucum�n", "Tucumán")
                                  .Replace("Jass�n", "Jassén")
                                  .Replace("Cama�o", "Camaño")
                                  .Replace("Mart�n", "Martín")
                                  .Replace("Emisi�n", "Emisión")
                                  .Replace("Fari�a", "Fariña")
                                  .Replace("Malarg�e", "Malargüe")
                                  .Replace("Mar�a", "María");
        }

        /// <summary>
        /// Gets the maximun entries that a search could return when the filter does not specify a country
        /// </summary>
        /// <returns></returns>
        private int GetTopEntriesSearch()
        {
            return Int32.Parse(System.Configuration.ConfigurationManager.AppSettings.Get("TopEntriesSearch"));
        }
    }
}