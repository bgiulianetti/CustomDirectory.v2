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

            if (first == null) first = string.Empty;
            if (last == null) last = string.Empty;
            if (countryCode == null) countryCode = string.Empty;
            if (number == null) number = string.Empty;
            if (start == null) start = "1";
            if (page == null) page = "1";
            #endregion

            //Paises con Cluster Dedicados (Argentina - Brasil - Paraguay)
            if (GetCountryCodesWithDedicatedCluster().Contains(countryCode))
            {
                var country = GetCountryByCode(countryCode);
                if (country == null)
                {
                    xmlOutput = FormatErrorMessage(ConfigurationManager.AppSettings.Get(language + ".ErrorNoMatches"),
                                                   ConfigurationManager.AppSettings.Get(language + ".InvalidCountryCode"));
                }
                else
                {
                    var stringSinglePageDirectory = string.Empty;
                    try
                    {
                        stringSinglePageDirectory = GetStringSinglePageDirectory(new HttpClient(), first, last, number, country.Name, start);
                        xmlOutput = FixFormatForSingleCountry(stringSinglePageDirectory, country, first, last, number, start);
                        xmlOutput = FixAccentuation(xmlOutput);
                    }
                    catch (Exception ex)
                    {
                        xmlOutput = FormatErrorMessage("Error", ex.Message);
                    }
                }
            }
            // Paises con prefijos con cluster compartido (todos menos Chile)
            else if (GetCountryCodesWithSharedClusterWithPrefixes().Contains(countryCode))
            {
                var directories = new List<IPPhoneDirectory>();
                try
                {
                    var country = GetCountryByCode(countryCode);
                    var ArrNumber = ConfigurationManager.AppSettings.Get(country.Name + ".Prefix").Split('-');
                    foreach (var itemNumber in ArrNumber)
                    {
                        directories.AddRange(GetAllDirectoriesForCountriesWithSharedClusterAndPrefixes(first, last, itemNumber, start, country));
                    }
                    if (directories.Count > 0)
                    {
                        var allEntries = GetEntriesOrderedAndWithPrefix(directories);
                        var selectedEntries = SelectEntriesByPage(allEntries, Int32.Parse(page));
                        xmlOutput = BuildXML(selectedEntries, first, last, number, page, allEntries.Count);
                        xmlOutput = FixAccentuation(xmlOutput);
                    }
                    else
                    {
                        xmlOutput = FormatErrorMessage(ConfigurationManager.AppSettings.Get(language + ".Error"),
                                                       ConfigurationManager.AppSettings.Get(language + ".ErrorNoMatches"));
                    }
                    
                }
                catch (Exception ex)
                {
                    xmlOutput = FormatErrorMessage("Error", ex.Message);
                }
            }
            //Paises sin prefijo con cluster compartido
            else if (GetCountryCodesWithSharedClusterWithOutPrefixes().Contains(countryCode))
            {
                //To Do !!
                //corro el metodo de obtener los paises de mi mismo cluster, y con sus prefijos voy descartando los que no van, 
                //los resultantes van a ser los que tengo que utilizar
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
                        xmlOutput = BuildXML(selectedEntries, first, last, number, page, allEntries.Count);
                        xmlOutput = FixAccentuation(xmlOutput);
                    }
                    else
                    {
                        xmlOutput = FormatErrorMessage(ConfigurationManager.AppSettings.Get(language + ".Error"),
                                                       ConfigurationManager.AppSettings.Get(language + ".ErrorNoMatches"));
                    }
                }
                catch (Exception ex)
                {
                    xmlOutput = FormatErrorMessage("Error", ex.Message);
                }
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
            var directoryUrl = GetDirectoryUrlByCountry(countryName.Replace(" ", "_"));
            if (directoryUrl == null)
                return null;

            client.BaseAddress = new Uri(directoryUrl);
            HttpResponseMessage response = null;
            var intStart = Int32.Parse(start);


            for (int i = 1; i <= intStart; i += 31)
            {
                var request = GenerateHttpRequestMassage(directoryUrl);
                request.RequestUri = new Uri(directoryUrl + "?l=" + last + "&f=" + first + "&n=" + number + "&start=" + i.ToString());
                try
                {
                    response = client.SendAsync(request).Result;
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
                if (!response.IsSuccessStatusCode)
                    throw new Exception(response.ReasonPhrase + " " + ConfigurationManager.AppSettings.Get(GetLanguageApplication() + ".GettingCountry") + ": " + countryName);
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
            var arrCountries = ConfigurationManager.AppSettings.Get("Countries").Split('|');
            for (int i = 0; i < arrCountries.Count(); i++)
            {
                var arrItem = arrCountries[i].Split(':');
                var country = new Country(name: arrItem[0],
                                          code: arrItem[1].ToUpper(),
                                          internalPrefix: arrItem[2].Split('-').ToList<string>(),
                                          externalPrefix: arrItem[3],
                                          cluster: arrItem[4]);
                countryList.Add(country);
            }
            return countryList;
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
            var language = GetLanguageApplication();
            return stringDirectory.Replace("<Name>", "<Name>[" + country.Code.ToUpper() + "] ")
                                  .Replace("<Prompt>Records", "<Prompt>" + ConfigurationManager.AppSettings.Get(language + ".Records"))
                                  .Replace(" to ", " " + ConfigurationManager.AppSettings.Get(language + ".To") + " ")
                                  .Replace(" of ", " " + ConfigurationManager.AppSettings.Get(language + ".Of") + " ")
                                  .Replace("<Name>[" + country.Code + "] Dial", "<Name>Dial")
                                  .Replace("<Name>[" + country.Code + "] Search", "<Name>Search")
                                  .Replace("<Name>[" + country.Code + "] Exit", "<Name>Exit")
                                  .Replace("<Name>[" + country.Code + "] EditDial", "<Name>EditDial")
                                  .Replace("<Name>[" + country.Code + "] Next", "<Name>Next")
                                  .Replace("<Telephone>", "<Telephone>" + country.ExternalPrefix)
                                  .Replace("<URL>" + GetUrlDirectoryByName(country.Name) + BuildQueryStringSearch(first, last, number, (Int32.Parse(start) + 31).ToString()).Replace("&", "&amp;") + "</URL>",
                                           "<URL>" + GetUrlCustomDirectory() + BuildQueryStringSearchWithCountryParameter(first, last, number, (Int32.Parse(start) + 31).ToString(), country.Code).Replace("&", "&amp;") + "</URL>")
                                  .Replace(("<URL>" + GetUrlDirectoryLandingByName(country.Name) + "</URL>").Replace("&f", "&amp;f").Replace("&n", "&amp;n").Replace("&start", "&amp;start"),
                                            "<URL>" + GetUrlDirectoryLandingByName(country.Name) + "</URL>").Replace("&f", "&amp;f").Replace("&n", "&amp;n").Replace("&start", "&amp;start")
                                  .Replace("<?xml version=\"1.0\"?>", "");
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
                List<IPPhoneDirectoryEntry> listEntries = null;
                try
                {
                    listEntries = GetDirectoryEntriesList(first, last, number, itemCountry.Name, start);
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }

                if(listEntries.Count > 0)
                {
                    IpPhoneDirectory.DirectoryEntries = listEntries;
                    IpPhoneDirectory.EntriesCount = listEntries.Count;
                    list.Add(IpPhoneDirectory);
                }
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
            return ConfigurationManager.AppSettings.Get("UrlDirectory." + countryName);
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
                    entry.Name = "[" + dir.Country.Code + "] " + entryItem.Name;
                    entry.Telephone = dir.Country.ExternalPrefix + entryItem.Telephone;
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
            xmlOutput += BuildSoftKey(SoftKey.EditDial.ToString(), "SoftKey:" + SoftKey.EditDial.ToString(), 2);
            xmlOutput += BuildSoftKey(SoftKey.Exit.ToString(), "SoftKey:" + SoftKey.Exit.ToString(), 3);
            if (showNext)
                xmlOutput += BuildSoftKey(SoftKey.Next.ToString(), GetUrlCustomDirectory() + BuildQueryStringSearch(first, last, number, "", (intPage + 1).ToString()).Replace("&f", "&amp;f").Replace("&n", "&amp;n").Replace("&start", "&amp;start").Replace("&page=", "&amp;page="), 4);
            xmlOutput += BuildSoftKey(SoftKey.Search.ToString(), GetUrlDirectoryLanding(), 5);
            xmlOutput += "</CiscoIPPhoneDirectory>";

            return xmlOutput;
        }

        /// <summary>
        /// Gets the Url Custom directory from the WebConfig
        /// </summary>
        /// <returns></returns>
        private string GetUrlCustomDirectory()
        {
            return ConfigurationManager.AppSettings.Get("UrlCustomDirectory");
        }

        /// <summary>
        /// Gets the Url directory from the WebConfig by the country name
        /// </summary>
        /// <param name="countryName"></param>
        /// <returns></returns>
        private string GetUrlDirectoryByName(string countryName)
        {
            return System.Configuration.ConfigurationManager.AppSettings.Get("UrlDirectory." + countryName);
        }

        /// <summary>
        /// Gets the Url directory landing from the WebConfig by the country name
        /// </summary>
        /// <param name="countryName"></param>
        /// <returns></returns>
        private string GetUrlDirectoryLandingByName(string countryName)
        {
            return System.Configuration.ConfigurationManager.AppSettings.Get("UrlDirectory.Landing." + countryName);
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
        private string FormatErrorMessage(string title, string message)
        {
            return "<Title>" + title + "</Title>" + Environment.NewLine +
                    "<Prompt/>" + Environment.NewLine +
                    "<Text>" + message + "</Text>";
        }

        /// <summary>
        /// Gets All entries of Peru, Uruguay or Colombia directories with a specific search criteria
        /// </summary>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <param name="number"></param>
        /// <param name="start"></param>
        /// <returns>List<IPPhoneDirectory></returns>
        private List<IPPhoneDirectory> GetAllDirectoriesForCountriesWithSharedClusterAndPrefixes(string first, string last, string number, string start, Country country)
        {
            var list = new List<IPPhoneDirectory>();
            var IpPhoneDirectory = new IPPhoneDirectory();
            IpPhoneDirectory.Country = country;
            List<IPPhoneDirectoryEntry> listEntries = null;
            try
            {
                listEntries = GetDirectoryEntriesListForCountriesWithSharedClusterAndPrefixes(first, last, number, country.Name, start);
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
        private List<IPPhoneDirectoryEntry> GetDirectoryEntriesListForCountriesWithSharedClusterAndPrefixes(string first, string last, string number, string country, string start)
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

        private List<string> GetCountryCodesWithDedicatedCluster()
        {
            return ConfigurationManager.AppSettings.Get("Countries.DedicatedCluster").Split('-').ToList<string>();
        }

        private List<string> GetCountryCodesWithSharedClusterWithPrefixes()
        {
            return ConfigurationManager.AppSettings.Get("Countries.SharedClusterWithPrefixes").Split('-').ToList<string>();
        }

        private List<string> GetCountryCodesWithSharedClusterWithOutPrefixes()
        {
            return ConfigurationManager.AppSettings.Get("Countries.SharedClusterWithOutPrefixes").Split('-').ToList<string>();
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
    }
}