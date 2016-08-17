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
using System.Configuration;
using Newtonsoft.Json;

namespace CustomDirectory.v2
{
    public partial class _CustomDirecotory : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            #region QueryStrings
            var xmlOutput = string.Empty;
            var language = GetLanguageApplication();
            var first = Request.QueryString["f"];
            var last = Request.QueryString["l"];
            var countryCode = Request.QueryString["p"];
            var number = Request.QueryString["n"];
            var start = Request.QueryString["start"];
            var page = Request.QueryString["page"];

            if (first == null || first.Replace(" ", "") == string.Empty)
                first = string.Empty;
            else
                first = ReplaceAccentuation(first);

            if (last == null || last.Replace(" ", "") == string.Empty)
                last = string.Empty;
            else
                last = ReplaceAccentuation(last);

            if (countryCode == null || countryCode.Replace(" ", "") == string.Empty)
                countryCode = string.Empty;
            else
                countryCode.ToLower();

            if (number == null) number = string.Empty;
            if (start == null) start = "1";
            if (page == null) page = "1";
            #endregion

            if (!string.IsNullOrEmpty(countryCode) && GetCountryByCode(countryCode) == null)
            {
                xmlOutput = FormatErrorMessage("Invalid Country");
            }
            //Paises con Cluster Dedicados
            else if (GetCountryCodesWithDedicatedCluster().Contains(countryCode))
            {
                var country = GetCountryByCode(countryCode);
                if (country == null)
                {
                    xmlOutput = FormatErrorMessage("Invalid Country");
                }
                else
                {
                    var stringSinglePageDirectory = string.Empty;
                    try
                    {
                        var url = GetClusterUrlByCountryName(country.Name);
                        stringSinglePageDirectory = GetStringSinglePageDirectory(new HttpClient(), first, last, number, url, start);
                        xmlOutput = FixFormatForSingleCountry(stringSinglePageDirectory, country, first, last, number, start);
                        xmlOutput = FixAccentuation(xmlOutput);
                    }
                    catch (Exception ex)
                    {
                        xmlOutput = FormatErrorMessage(ex.Message);
                    }
                }
            }
            // Paises con prefijos con cluster compartido
            else if (GetCountryCodesWithSharedClusterWithPrefixes().Contains(countryCode))
            {
                var directories = new List<IPPhoneDirectory>();
                try
                {
                    var country = GetCountryByCode(countryCode);
                    var url = GetClusterUrlByCountryName(country.Name);
                    foreach (var itemNumber in country.InternalPrefix)
                    {
                        directories.AddRange(GetAllDirectoriesForCountriesWithSameClusterAndWithPrefixes(first, last, itemNumber, start, country));
                    }
                    if (directories.Count > 0)
                    {
                        var allEntries = GetEntriesOrderedAndWithPrefix(directories);
                        var entriesOfTheCountry = new List<IPPhoneDirectoryEntry>();
                        foreach (var entry in allEntries)
                        {
                            if (entry.Name.StartsWith("[" + country.Code.ToUpper() + "]"))
                                entriesOfTheCountry.Add(entry);
                        }
                        var selectedEntries = SelectEntriesByPage(entriesOfTheCountry, Int32.Parse(page));
                        xmlOutput = BuildXML(selectedEntries, first, last, number, page, entriesOfTheCountry.Count, country.Code);
                        xmlOutput = FixAccentuation(xmlOutput);
                    }
                    else
                    {
                        xmlOutput = FormatErrorMessage("No Matches");
                    }

                }
                catch (Exception ex)
                {
                    xmlOutput = FormatErrorMessage(ex.Message);
                }
            }
            //Paises sin prefijo con cluster compartido
            else if (GetCountryCodesWithSharedClusterWithOutPrefixes().Contains(countryCode))
            {
                List<IPPhoneDirectory> directories = null;
                try
                {
                    directories = GetAllDirectories(first, last, number, start);
                    if (directories.Count > 0)
                    {
                        var _allEntries = GetEntriesOrderedAndWithPrefix(directories);
                        var allEntries = new List<IPPhoneDirectoryEntry>();
                        if (countryCode == "cl")
                        {
                            for (int i = 0; i < _allEntries.Count; i++)
                            {
                                if (_allEntries[i].Name.Contains("[CL]"))
                                    allEntries.Add(_allEntries[i]);
                            }
                        }
                        else
                        {
                            allEntries = _allEntries;
                        }
                        var selectedEntries = SelectEntriesByPage(allEntries, Int32.Parse(page));
                        xmlOutput = BuildXML(selectedEntries, first, last, number, page, allEntries.Count, string.Empty);
                        xmlOutput = FixAccentuation(xmlOutput);
                    }
                    else
                    {
                        xmlOutput = FormatErrorMessage("No Matches");
                    }
                }
                catch (Exception ex)
                {
                    xmlOutput = FormatErrorMessage(ex.Message);
                }
            }
            //Todos los paises de todos los clusters
            else if (countryCode == string.Empty)
            {
                List<IPPhoneDirectory> directories = null;
                try
                {
                    directories = GetAllDirectories(first, last, number, start);
                    if (directories.Count > 0)
                    {
                        var allEntries = GetEntriesOrderedAndWithPrefix(directories);
                        var selectedEntries = SelectEntriesByPage(allEntries, Int32.Parse(page));
                        xmlOutput = BuildXML(selectedEntries, first, last, number, page, allEntries.Count, string.Empty);
                        xmlOutput = FixAccentuation(xmlOutput);
                    }
                    else
                    {
                        xmlOutput = FormatErrorMessage("No Matches");
                    }
                }

                catch (Exception ex)
                {
                    xmlOutput = FormatErrorMessage(ex.Message);
                }
            }

            xmlOutput = FixAccentuation(xmlOutput);
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
        private string GetStringSinglePageDirectory(HttpClient client, string first, string last, string number, string clusterUrl, string start)
        {
            client.BaseAddress = new Uri(clusterUrl);
            HttpResponseMessage response = null;
            var intStart = Int32.Parse(start);

            for (int i = 1; i <= intStart; i += 31)
            {
                var request = GenerateHttpRequestMassage(clusterUrl);
                request.RequestUri = new Uri(clusterUrl + "?l=" + last + "&f=" + first + "&n=" + number + "&start=" + i.ToString());
                try
                {
                    response = client.SendAsync(request).Result;
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
                if (!response.IsSuccessStatusCode)
                    throw new Exception(response.ReasonPhrase + " " + ConfigurationManager.AppSettings.Get(GetLanguageApplication() + ".GettingCountry"));
            }
            return response.Content.ReadAsStringAsync().Result;
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
        /// Builds a string format to add a Directory URL as a QueryString to perform a search including de parameter country [p]
        /// </summary>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="number"></param>
        /// <param name="start"></param>
        /// <param name="country"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        private string BuildQueryStringSearchWithCountryParameter(string first, string last, string number, string start, string country, string page = null)
        {
            if (page == null)
                return "?l=" + last + "&f=" + first + "&n=" + number + "&p=" + country + "&start=" + start;
            else
                return "?l=" + last + "&f=" + first + "&n=" + number + "&p=" + country + "&page=" + page;
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
        private string FixFormatForSingleCountry(string stringDirectory, Country country, string first, string last, string number, string start)
        {
            var hasDial = false;
            var hasEditDial = false;
            var hasExit = false;
            var hasNext = false;
            var hasSearch = false;
            var entriesList = new List<IPPhoneDirectoryEntry>();
            var language = GetLanguageApplication();
            var prompt = string.Empty;


            if (stringDirectory.Contains("<Name>Dial</Name>"))
                hasDial = true;
            if (stringDirectory.Contains("<Name>EditDial</Name>"))
                hasEditDial = true;
            if (stringDirectory.Contains("<Name>Exit</Name>"))
                hasExit = true;
            if (stringDirectory.Contains("<Name>Next</Name>"))
                hasNext = true;
            if (stringDirectory.Contains("<Name>Search</Name>"))
                hasSearch = true;

            var promptLocation = stringDirectory.IndexOf("<Prompt>", 0) + 8;
            var stringPrompt = string.Empty;
            while (stringDirectory[promptLocation] != '<')
            {
                stringPrompt += stringDirectory[promptLocation].ToString();
                promptLocation++;
            }

            stringDirectory = DeleteBottomMenu(stringDirectory).Replace("<DirectoryEntry>", "#").Replace("</DirectoryEntry>", "");
            var arrayEntries = stringDirectory.Split('#');
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
                    var name = arrayEntry[0].Replace("\r\n", string.Empty).TrimStart();
                    if (name != null && ("[xx] " + name).Length > 32)
                    {
                        while (("[xx] " + name).Length > 32)
                            name = name.Substring(0, name.Length - 1);
                    }
                    IpEntry.Name = name;
                    IpEntry.Telephone = country.ExternalPrefix + arrayEntry[1].Replace(" ", string.Empty).Replace("\r\n", string.Empty);
                    entriesList.Add(IpEntry);
                }
            }


            var XmlPageDirectory = "<CiscoIPPhoneDirectory>" + Environment.NewLine;
            foreach (var item in entriesList)
            {
                item.Name = "[" + country.Code.ToUpper() + "] " + item.Name;
                if (item.Name.Length > 32)
                    item.Name = item.Name.Substring(0, item.Name.Length - 1);
                XmlPageDirectory += item.ToString();
            }

            XmlPageDirectory += Environment.NewLine + "<Prompt>" + stringPrompt + "</Prompt>" + Environment.NewLine;

            if(hasDial)
                XmlPageDirectory += BuildSoftKey(SoftKey.Dial.ToString(), "SoftKey:" + SoftKey.Dial.ToString(), 1);
            if(hasEditDial)
                XmlPageDirectory += BuildSoftKey(SoftKey.Cancel.ToString(), "SoftKey:" + SoftKey.Cancel.ToString(), 2);
            if(hasExit)
                XmlPageDirectory += BuildSoftKey(SoftKey.Exit.ToString(), "SoftKey:" + SoftKey.Exit.ToString(), 3);
            if(hasNext)
                XmlPageDirectory += BuildSoftKey(SoftKey.Next.ToString(),  GetUrlLocalHost() + BuildQueryStringSearchWithCountryParameter(first, last, number, (Int32.Parse(start) + 31).ToString(), country.Code).Replace("&", "&amp;"), 4);
            if(hasSearch)
                XmlPageDirectory += BuildSoftKey(SoftKey.Search.ToString(), GetUrlLocalHostLanding(), 5);

            XmlPageDirectory += Environment.NewLine + "</CiscoIPPhoneDirectory>";

            return XmlPageDirectory;
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
                if (!isFirstTime)
                {
                    var intStart = Int32.Parse(start) + 31;
                    start = intStart.ToString();
                }
                else
                {
                    isFirstTime = false;
                }

                var directoryPage = string.Empty;
                try
                {
                    directoryPage = GetStringSinglePageDirectory(new HttpClient(), first, last, number, country, start);
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }

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
                        var name = arrayEntry[0].Replace("\r\n", string.Empty).TrimStart();
                        if (name != null && ("[xx] " + name).Length > 32)
                        {
                            while (("[xx] " + name).Length > 32)
                                name = name.Substring(0, name.Length - 1);
                        }
                        IpEntry.Name = name;
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
            var clusters = GetAvailableClusters();
            foreach (var itemCluster in clusters)
            {
                var IpPhoneDirectory = new IPPhoneDirectory();
                IpPhoneDirectory.Cluster = null;
                List<IPPhoneDirectoryEntry> listEntries = null;
                try
                {
                    listEntries = GetDirectoryEntriesList(first, last, number, GetClusterUrlByClusterName(itemCluster.Name), start);
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }

                if (listEntries.Count > 0)
                {
                    IpPhoneDirectory.DirectoryEntries = listEntries;
                    IpPhoneDirectory.EntriesCount = listEntries.Count;
                    IpPhoneDirectory.Cluster = itemCluster;
                    list.Add(IpPhoneDirectory);
                }
            }
            return list;
        }

        /// <summary>
        /// Gets a List of entries ordered alphabetically and with cpuntry prefix country added from a list of directories
        /// </summary>
        /// <param name="directories">List<IPPhoneDirectory></param>
        /// <returns>List<IPPhoneDirectoryEntry></returns>
        private List<IPPhoneDirectoryEntry> GetEntriesOrderedAndWithPrefix(List<IPPhoneDirectory> directories)
        {
            var listOrderedWithPrefixes = new List<IPPhoneDirectoryEntry>();
            foreach (var directory in directories)
            {
                foreach (var entryItem in directory.DirectoryEntries)
                {
                    if (!string.Equals(entryItem.Telephone, string.Empty, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var country = GetCountryFromDirectoryEntryAndDirectoryClusterName(entryItem, directory.Cluster.Name);
                        if (country != null)
                        {
                            var entry = new IPPhoneDirectoryEntry();
                            entry.Name = "[" + country.Code.ToUpper() + "] " + entryItem.Name;
                            entry.Telephone = country.ExternalPrefix + entryItem.Telephone;
                            listOrderedWithPrefixes.Add(entry);
                        }
                    }
                }
            }
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
        private string BuildXML(List<IPPhoneDirectoryEntry> entries, string first, string last, string number, string page, int totalEntries, string countryCode)
        {
            var language = GetLanguageApplication();
            var xmlOutput = "<CiscoIPPhoneDirectory>" + Environment.NewLine;
            foreach (var item in entries)
            {
                xmlOutput += item.ToString();
            }

            var intPage = Int32.Parse(page);
            int start = ((intPage - 1) * 31) + 1;

            var entriesPerPage = 0;
            var showNext = true;
            if (totalEntries <= 31)
            {
                //Pagina de una busqueda con menos de 31 entradas
                entriesPerPage = totalEntries;
                showNext = false;
            }
            else
            {
                if (totalEntries - start > 31)
                {
                    //Pagina de una busqueda de mas de 31 entradas
                    entriesPerPage = start + 30;
                }
                else
                {
                    //Ultima pagina de una busqueda de mas de 31 entradas
                    entriesPerPage = totalEntries;
                    showNext = false;
                }
            }
            xmlOutput += "<Prompt>" + ConfigurationManager.AppSettings.Get(language + ".Records") + " " + start.ToString() + " a " + (entriesPerPage).ToString() + " de " + totalEntries.ToString() + "</Prompt>" + Environment.NewLine;
            xmlOutput += BuildSoftKey(SoftKey.Dial.ToString(), "SoftKey:" + SoftKey.Dial.ToString(), 1);
            xmlOutput += BuildSoftKey(SoftKey.Cancel.ToString(), "SoftKey:" + SoftKey.Cancel.ToString(), 2);
            xmlOutput += BuildSoftKey(SoftKey.Exit.ToString(), "SoftKey:" + SoftKey.Exit.ToString(), 3);
            if (showNext)
            {
                if (countryCode != string.Empty)
                {
                    var urlReplacedAmpersand = GetUrlLocalHost() + BuildQueryStringSearchWithCountryParameter(first, last, number, "", countryCode, (intPage + 1).ToString()).Replace("&f", "&amp;f").Replace("&n", "&amp;n").Replace("&start", "&amp;start").Replace("&page=", "&amp;page=").Replace("&p", "&amp;p");
                    xmlOutput += BuildSoftKey(SoftKey.Next.ToString(), urlReplacedAmpersand, 4);
                }
                else
                {
                    var urlReplacedAmpersand = GetUrlLocalHost() + BuildQueryStringSearch(first, last, number, "", (intPage + 1).ToString()).Replace("&f", "&amp;f").Replace("&n", "&amp;n").Replace("&start", "&amp;start").Replace("&page=", "&amp;page=");
                    xmlOutput += BuildSoftKey(SoftKey.Next.ToString(), urlReplacedAmpersand, 4);
                }
            }
            xmlOutput += BuildSoftKey(SoftKey.Search.ToString(), GetUrlLocalHostLanding(), 5);
            xmlOutput += "</CiscoIPPhoneDirectory>";

            return xmlOutput;
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
                                  .Replace("Mar�a", "María")
                                  .Replace("D�az", "Díaz")
                                  .Replace("B�rtolo", "Bértolo")
                                  .Replace("Mart�n", "Martín");
        }

        /// <summary>
        /// Gets the maximun entries that a search could return when the filter does not specify a country
        /// </summary>
        /// <returns></returns>
        private int GetTopEntriesSearch()
        {
            return Int32.Parse(System.Configuration.ConfigurationManager.AppSettings.Get("TopEntriesSearch"));
        }

        /// <summary>
        /// Generates a request adding user agent, accept encoding, and accept language
        /// </summary>
        /// <param name="directoryUrl"></param>
        /// <returns></returns>
        private HttpRequestMessage GenerateHttpRequestMassage(string directoryUrl)
        {
            var request = new HttpRequestMessage();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            request.Headers.Add("Accept-Encoding", "gzip, deflate, sdch");
            request.Headers.Add("Accept-Language", "es,en;q=0.8,en-US;q=0.6,pt-BR;q=0.4,pt;q=0.2,es-419;q=0.2,de;q=0.2");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.86 Safari/537.36");

            return request;
        }

        /// <summary>
        /// Gets the language of the aplication. English[EN] or Spanish[ES]
        /// </summary>
        /// <returns></returns>
        private string GetLanguageApplication()
        {
            return System.Configuration.ConfigurationManager.AppSettings.Get("Language");
        }

        /// <summary>
        /// Formatex and xml of an error with its title and message
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private string FormatErrorMessage(string message)
        {
            return "<CiscoIPPhoneDirectory><Prompt>" + message + "</Prompt></CiscoIPPhoneDirectory>";
        }

        /// <summary>
        /// Gets All entries of Peru, Uruguay or Colombia directories with a specific search criteria
        /// </summary>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="number"></param>
        /// <param name="start"></param>
        /// <returns>List<IPPhoneDirectory></returns>
        private List<IPPhoneDirectory> GetAllDirectoriesForCountriesWithSameClusterAndWithPrefixes(string first, string last, string number, string start, Country country)
        {
            var list = new List<IPPhoneDirectory>();
            var IpPhoneDirectory = new IPPhoneDirectory();
            var clusterUrl = GetClusterUrlByClusterName(country.Cluster);
            IpPhoneDirectory.Cluster = GetClusterByName(country.Cluster);
            List<IPPhoneDirectoryEntry> listEntries = null;
            try
            {
                listEntries = GetDirectoryEntriesListForCountriesWithSharedClusterAndPrefixes(first, last, number, clusterUrl, start);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            if (listEntries.Count > 0)
            {
                IpPhoneDirectory.DirectoryEntries = listEntries;
                IpPhoneDirectory.EntriesCount = listEntries.Count;
                list.Add(IpPhoneDirectory);
            }
            return list;
        }

        /// <summary>
        /// Gets all the entries of a Directories from Colombia, Peru and Uruguay with a specific search criteria
        /// </summary>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="number"></param>
        /// <param name="country"></param>
        /// <param name="start"></param>
        /// <returns>List<IPPhoneDirectoryEntry></returns>
        private List<IPPhoneDirectoryEntry> GetDirectoryEntriesListForCountriesWithSharedClusterAndPrefixes(string first, string last, string number, string clusterUrl, string start)
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
                if (!isFirstTime)
                {
                    var intStart = Int32.Parse(start) + 31;
                    start = intStart.ToString();
                }
                else
                {
                    isFirstTime = false;
                }

                var directoryPage = string.Empty;
                try
                {
                    directoryPage = GetStringSinglePageDirectory(new HttpClient(), first, last, number, clusterUrl, start);
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }

                if (!directoryPage.Contains("<Name>Next</Name>"))
                    isEmpty = true;
                if (directoryPage.Contains("<DirectoryEntry>"))
                {
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
                }
                interruptionLoopTooLong++;
            }
            return list;
        }




        //Countries
        private List<Country> GetAvailableCountries()
        {
            using (StreamReader r = new StreamReader(Server.MapPath("~/Resources/Countries.Metadata/" + GetCountriesFileName())))
            {
                string json = r.ReadToEnd();
                List<Country> items = JsonConvert.DeserializeObject<List<Country>>(json);
                return items;
            }
        }

        private string GetCountriesFileName()
        {
            return ConfigurationManager.AppSettings.Get("Countries.FileName");
        }

        private List<string> GetCountryCodesWithDedicatedCluster()
        {
            var countries = GetAvailableCountries();
            var countryCodesList = new List<string>();
            foreach (var country in countries)
            {
                var isDedicated = true;
                for (int i = 0; i < countries.Count; i++)
                {
                    if (country.Cluster == countries[i].Cluster && country.Name != countries[i].Name)
                    {
                        isDedicated = false;
                        break;
                    }
                }
                if (isDedicated)
                    countryCodesList.Add(country.Code);
            }
            return countryCodesList;
        }

        private List<string> GetCountryCodesWithSharedClusterWithPrefixes()
        {
            var countries = GetAvailableCountries();
            var countryCodesList = new List<string>();
            foreach (var country in countries)
            {
                var isDedicated = true;
                for (int i = 0; i < countries.Count; i++)
                {
                    if (country.Cluster == countries[i].Cluster && country.Name != countries[i].Name)
                    {
                        isDedicated = false;
                        break;
                    }
                }
                if (!isDedicated && country.InternalPrefix.Count > 0)
                    countryCodesList.Add(country.Code);
            }
            return countryCodesList;
        }

        private List<string> GetCountryCodesWithSharedClusterWithOutPrefixes()
        {
            var countries = GetAvailableCountries();
            var countryCodesList = new List<string>();
            foreach (var country in countries)
            {
                var isDedicated = true;
                for (int i = 0; i < countries.Count; i++)
                {
                    if (country.Cluster == countries[i].Cluster && country.Name != countries[i].Name)
                    {
                        isDedicated = false;
                        break;
                    }
                }
                if (!isDedicated && country.InternalPrefix.Count == 0)
                    countryCodesList.Add(country.Code);
            }
            return countryCodesList;
        }

        private List<Country> GetCountriesFromSameCluster(string countryCode)
        {
            var myCluster = GetCountryByCode(countryCode).Cluster;
            var listCountry = new List<Country>();
            var countries = GetAvailableCountries();
            foreach (var country in countries)
            {
                if (country.Cluster == myCluster)
                    listCountry.Add(country);
            }
            return listCountry;
        }

        private List<Country> GetCountriesFromCluster(string clusterName)
        {
            var countriesList = new List<Country>();
            var countries = GetAvailableCountries();
            foreach (var country in countries)
            {
                if (country.Cluster == clusterName)
                    countriesList.Add(country);
            }
            return countriesList;
        }

        private Country GetCountryWithoutPrefixFromClusterName(string clusterName)
        {
            var countries = GetCountriesFromCluster(clusterName);
            foreach (var country in countries)
            {
                if (country.InternalPrefix.Count == 0)
                    return country;
            }
            return null;
        }

        private Country GetCountryFromDirectoryEntryAndDirectoryClusterName(IPPhoneDirectoryEntry entry, string clusterName)
        {
            var countries = GetCountriesFromCluster(clusterName);
            Country countryReponse = null;
            foreach (var country in countries)
            {
                foreach (var prefix in country.InternalPrefix)
                {
                    var numberLengthWithPrefix = prefix.Length + Int32.Parse(country.NumberLength);
                    if (entry.Telephone.StartsWith(prefix) && entry.Telephone.Length == numberLengthWithPrefix)
                    {
                        countryReponse = country;
                        break;
                    }
                }
                if (countryReponse != null)
                    break;
            }
            if (countryReponse == null)
                return GetCountryWithoutPrefixFromClusterName(clusterName);
            else
                return countryReponse;
        }

        /// <summary>
        /// Gets the name of a country by it´s code
        /// </summary>
        /// <param name="countryCode"></param>
        /// <returns></returns>
        private Country GetCountryByCode(string countryCode)
        {
            var countries = GetAvailableCountries();
            foreach (var countryItem in countries)
            {
                if (string.Equals(countryCode, countryItem.Code, StringComparison.InvariantCultureIgnoreCase))
                    return countryItem;
            }
            return null;
        }

        private Country GetCountryByName(string countryName)
        {
            var countries = GetAvailableCountries();
            foreach (var countryItem in countries)
            {
                if (string.Equals(countryName, countryItem.Name, StringComparison.InvariantCultureIgnoreCase))
                    return countryItem;
            }
            return null;
        }





        //Clusters
        private string GetIpAdressFromClusterName(string clusterName)
        {
            var cluster = GetClusterByName(clusterName);
            return cluster.IPAdress;
        }

        private List<Cluster> GetAvailableClusters()
        {
            using (StreamReader r = new StreamReader(Server.MapPath("~/Resources/" + GetClustersFileName())))
            {
                string json = r.ReadToEnd();
                List<Cluster> items = JsonConvert.DeserializeObject<List<Cluster>>(json);
                return items;
            }
        }

        private Cluster GetClusterByName(string clusterName)
        {
            var clusters = GetAvailableClusters();
            foreach (var cluster in clusters)
            {
                if (cluster.Name == clusterName)
                    return cluster;
            }
            return null;
        }

        private string GetClustersFileName()
        {
            return ConfigurationManager.AppSettings.Get("Clusters.FileName");
        }

        /// <summary>
        /// Gets the Url directory landing from the WebConfig by the country name
        /// </summary>
        /// <param name="countryName"></param>
        /// <returns></returns>
        private string GetClusterLandingUrlByCountryName(string countryName)
        {
            var clusterName = GetCountryByName(countryName).Cluster;
            var cluster = GetClusterByName(clusterName);
            var format = System.Configuration.ConfigurationManager.AppSettings.Get("UrlDirectory.Landing.Format");
            return string.Format(format, cluster.IPAdress);
        }

        /// <summary>
        /// Gets the directory URL by the country name
        /// </summary>
        /// <param name="country">Name of the country</param>
        /// <returns>string</returns>
        private string GetClusterUrlByCountryName(string countryName)
        {
            var clusterName = GetCountryByName(countryName).Cluster;
            var urlFormat = ConfigurationManager.AppSettings.Get("UrlDirectory.Format");
            return string.Format(urlFormat, GetIpAdressFromClusterName(clusterName));
        }

        /// <summary>
        /// Gets the directory URL by the country name
        /// </summary>
        /// <param name="country">Name of the country</param>
        /// <returns>string</returns>
        private string GetClusterUrlByClusterName(string clusterName)
        {
            var urlFormat = ConfigurationManager.AppSettings.Get("UrlDirectory.Format");
            return string.Format(urlFormat, GetIpAdressFromClusterName(clusterName));
        }




        //LocalHost

        /// <summary>
        /// Gets the Url Custom directory from the WebConfig
        /// </summary>
        /// <returns></returns>
        private string GetUrlLocalHost()
        {
            return ConfigurationManager.AppSettings.Get("UrlCustomDirectory");
        }

        /// <summary>
        /// Gets the Url directory landing from the WebConfig
        /// </summary>
        /// <param name="countryName"></param>
        /// <returns></returns>
        private string GetUrlLocalHostLanding()
        {
            return System.Configuration.ConfigurationManager.AppSettings.Get("UrlCustomDirectory.Landing");
        }

        private string ReplaceAccentuation(string text)
        {
            text.ToLower();
            return text.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u");
        }

    }
}