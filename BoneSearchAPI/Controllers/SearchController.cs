using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using System.Data;
using System.Text.Encodings.Web;
using System.Web;

namespace BoneSearchAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private const string CONNECTION_STRING = "server=localhost;database=searchv3;user=api;password=;Pooling=true;minpoolsize=5;maxpoolsize=20;";

        [HttpGet]
        public IEnumerable<SearchResult> Get(string terms)
        {
            //throw an error if terms is null
            if (terms == null)
            {
                throw new ArgumentNullException(nameof(terms));
            }

            //if terms is empty, throw another error
            if (terms == string.Empty)
            {
                throw new ArgumentException("Terms cannot be empty", nameof(terms));
            }

            //declare MySQL connection
            using MySqlConnection con = new MySqlConnection(CONNECTION_STRING);
            con.Open();

            List<int> wordID = ConvertStringToIDs(con, terms);
            //if the wordID list is empty, throw an error
            if (wordID.Count == 0)
            {
                throw new Exception("No results found");
            }
            Dictionary<int, int> wordRelevance = GetWordRelevance(con, wordID);
            List<SearchResult> searchResults = ConvertPageIDsToSearchResults(con, wordRelevance);

            //set the response type to application/json
            Response.ContentType = "application/json";

            return searchResults;
        }

        //function to convert category ID to category name
        //single use function, connection disposes after conversion
        private string ConvertCategoryIDToName(int categoryID)
        {
            //duplicate the connection
            using MySqlConnection con2 = new MySqlConnection(CONNECTION_STRING);
            con2.Open(); //open the connection

            //create mysqlcommand object
            using MySqlCommand cmd = new MySqlCommand("SELECT name FROM category WHERE id=@category_id", con2);

            //bind the parameter
            cmd.Parameters.AddWithValue("@category_id", categoryID);

            //execute the query
            var reader = cmd.ExecuteReader();

            //read the result
            reader.Read();
            string category = reader.GetString("name");

            //close the reader
            reader.Close();

            return category;
        }

        private List<SearchResult> ConvertPageIDsToSearchResults(MySqlConnection con, Dictionary<int, int> pageIDs)
        {
            List<SearchResult> result = new List<SearchResult>();

            //loop through each pageID in the dictionary
            foreach (KeyValuePair<int, int> entry in pageIDs)
            {
                //create mysqlcommand object
                //TODO: modify this command to use "where in" instead of just "where"
                MySqlCommand cmd = new MySqlCommand("SELECT domain.name as domain_name, domain.https as domain_https, domain.category_id as domain_category, path, title, meta_desc, crawl_date FROM page JOIN domain ON page.domain_id = domain.id WHERE page.id=@page_id limit 10;", con);

                //bind the parameter
                cmd.Parameters.AddWithValue("@page_id", entry.Key);

                //execute the query
                var reader = cmd.ExecuteReader();

                //read the result
                while (reader.Read())
                {
                    SearchResult searchResult = new SearchResult();

                    searchResult.title = reader.GetString("title");
                    //entitize the title
                    searchResult.title = System.Net.WebUtility.HtmlEncode(searchResult.title);

                    searchResult.https = reader.GetBoolean("domain_https");

                    searchResult.domain = reader.GetString("domain_name");

                    searchResult.path = reader.GetString("path");

                    searchResult.metadesc = reader.GetString("meta_desc");
                    //entitize the metadesc
                    searchResult.metadesc = System.Net.WebUtility.HtmlEncode(searchResult.metadesc);

                    //get the date
                    try
                    {
                        searchResult.date = reader.GetDateTime("crawl_date").ToString("yyyy-MMM-dd");
                    } catch (Exception)
                    {
                        searchResult.date = null;
                    }

                    //try to get the category ID of the domain
                    try
                    {
                        searchResult.category = ConvertCategoryIDToName(reader.GetInt32("domain_category"));
                    }
                    catch (Exception)
                    {
                        searchResult.category = "&quest;";
                    }

                    result.Add(searchResult);
                }

                //close the reader
                reader.Close();

                //if the results are greater than 10, break out of the loop
                if (result.Count > 10)
                {
                    break;
                }
            }

            return result;
        }

        //Get word relevance from list of word IDs
        private Dictionary<int, int> GetWordRelevance(MySqlConnection con, List<int> wordID)
        {
            Dictionary<int, int> wordRelevance = new Dictionary<int, int>();

            //create the command string
            string cmdString = "SELECT page_id, sum(score) as total_score FROM word_relevance WHERE word_id IN (";

            //iterate through the wordID list
            for (int i = 0; i < wordID.Count; i++)
            {
                cmdString += "@" + i;

                //if the next iteration is the length of the array, then we don't need a comma
                if (i != wordID.Count - 1)
                {
                    cmdString += ", ";
                }
            }

            //finish the command string
            cmdString += ") group by page_id ORDER BY total_score DESC";

            //create mysqlcommand object
            MySqlCommand cmd = new MySqlCommand(cmdString, con);
            
            //bind the parameters
            for (int i = 0; i < wordID.Count; i++)
            {
                cmd.Parameters.AddWithValue("@" + i, wordID[i]);
            }

            //execute the query
            var reader = cmd.ExecuteReader();

            //read the result
            while (reader.Read())
            {
                int page_id = reader.GetInt32("page_id");
                int score = reader.GetInt32("total_score");

                if (wordRelevance.ContainsKey(page_id))
                {
                    wordRelevance[page_id] += score;
                }
                else
                {
                    wordRelevance.Add(page_id, score);
                }
            }

            //close the reader
            reader.Close();

            return wordRelevance;
        }

        private List<int> ConvertStringToIDs(MySqlConnection con, string _input)
        {
            string[] words = _input.Split(' ');
            List<int> wordID = new List<int>();

            //command string
            string cmdString = "SELECT id FROM word WHERE word IN (";

            //iterate through each word
            for (int i = 0; i < words.Length; i++)
            {
                cmdString += "@" + i;

                //if the next iteration is the length of the array, then we don't need a comma
                if (i != words.Length - 1)
                {
                    cmdString += ", ";
                }
            }

            //close the command string
            cmdString += ");";

            //create mysqlcommand object
            MySqlCommand cmd = new MySqlCommand(cmdString, con);

            //bind the parameters
            for (int i = 0; i < words.Length; i++)
            {
                cmd.Parameters.AddWithValue("@" + i, words[i]);
            }

            //execute the query
            var reader = cmd.ExecuteReader();

            //read the result
            while (reader.Read())
            {
                wordID.Add(reader.GetInt32(0));
            }

            reader.Close();

            return wordID;
        }
    }
}
