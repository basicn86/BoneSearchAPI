using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace BoneSearchAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private const string CONNECTION_STRING = "server=localhost;database=searchv2;user=api;password=";

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

            List<int> wordID = ConvertStringToIDs(terms);
            Dictionary<int, int> wordRelevance = GetWordRelevance(wordID);
            List<SearchResult> searchResults = ConvertPageIDsToSearchResults(wordRelevance);

            //set the response type to application/json
            Response.ContentType = "application/json";

            return searchResults;
        }
        
        private List<SearchResult> ConvertPageIDsToSearchResults(Dictionary<int, int> pageIDs)
        {
            List<SearchResult> result = new List<SearchResult>();
            using MySqlConnection con = new MySqlConnection(CONNECTION_STRING);
            con.Open();


            //loop through each pageID in the dictionary
            foreach (KeyValuePair<int, int> entry in pageIDs)
            {
                //create mysqlcommand object
                //TODO: modify this command to use "where in" instead of just "where"
                MySqlCommand cmd = new MySqlCommand("SELECT domain.name as domain_name, domain.https as domain_https, path, title FROM page JOIN domain ON page.domain_id = domain.id WHERE page.id=@page_id limit 10;", con);

                //bind the parameter
                cmd.Parameters.AddWithValue("@page_id", entry.Key);

                //execute the query
                var reader = cmd.ExecuteReader();

                //read the result
                while (reader.Read())
                {
                    SearchResult searchResult = new SearchResult();
                    searchResult.title = reader.GetString("title");
                    searchResult.https = reader.GetBoolean("domain_https");
                    searchResult.domain = reader.GetString("domain_name");
                    searchResult.path = reader.GetString("path");
                    
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
        private Dictionary<int, int> GetWordRelevance(List<int> wordID)
        {
            Dictionary<int, int> wordRelevance = new Dictionary<int, int>();

            using MySqlConnection con = new MySqlConnection(CONNECTION_STRING);
            con.Open();

            foreach (int word in wordID)
            {
                //create mysqlcommand object
                MySqlCommand cmd = new MySqlCommand("SELECT page_id, score FROM word_relevance WHERE word_id=@word_id ORDER BY score DESC", con);

                //bind the parameter
                cmd.Parameters.AddWithValue("@word_id", word);

                //execute the query
                var reader = cmd.ExecuteReader();

                //read the result
                while (reader.Read())
                {
                    int page_id = reader.GetInt32("page_id");
                    int score = reader.GetInt32("score");

                    if (wordRelevance.ContainsKey(page_id))
                    {
                        wordRelevance[page_id] += score;
                    }else
                    {
                        wordRelevance.Add(page_id, score);
                    }
                }

                //close the reader
                reader.Close();
            }

            return wordRelevance;
        }
       
        private List<int> ConvertStringToIDs(string _input)
        {
            string[] words = _input.Split(' ');
            List<int> wordID = new List<int>();

            using MySqlConnection con = new MySqlConnection(CONNECTION_STRING);
            con.Open();

            foreach (string word in words)
            {
                //create mysqlcommand object
                MySqlCommand cmd = new MySqlCommand("SELECT id FROM word WHERE word.word = @word", con);

                //bind the parameter
                cmd.Parameters.AddWithValue("@word", word);

                //execute the query
                var reader = cmd.ExecuteReader();

                //read the result
                while (reader.Read())
                {
                    wordID.Add(reader.GetInt32(0));
                }

                reader.Close();
            }

            con.Close();

            return wordID;
        }
    }
}
