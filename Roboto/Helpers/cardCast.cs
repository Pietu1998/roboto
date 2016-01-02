﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Text;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Roboto.Helpers
{
    public class cardcast_question_card
    {
        public string question;
        public int nrAnswers;
    }

    public class cardcast_answer_card
    {
        public string answer;
        public int nrAnswers;
    }

    public class cardcast_pack
    {
        public Guid packID = Guid.NewGuid();
        public string name;
        public string packCode;
        public string description;
        public DateTime lastSynced = DateTime.MinValue;

        internal cardcast_pack() { }
        public cardcast_pack(string name, string packCode, string description)
        {
            this.name = name;
            this.packCode = packCode;
            this.description = description;
        }        
    }

    /// <summary>
    /// Helpder methods for working with cardcast
    /// </summary>
    public static class cardCast
    {
        const string cardCastURL = "https://api.cardcastgame.com/v1/decks/";

        /// <summary>
        /// Boilerplate text for working with cardcast packs
        /// </summary>
        /// <returns></returns>
        public static string boilerPlate =
                "Cardcast packs are grabbed from cardcastgame.com - you should search for new deck codes (or create your own) there.";
        
        /// <summary>
        /// Get the cards and pack info from cardcast
        /// </summary>
        /// <param name="packCode"></param>
        /// <param name="questions"></param>
        /// <param name="answers"></param>
        public static bool getPackCards(ref string packCode, out cardcast_pack packData, ref List<cardcast_question_card> questions, ref List<cardcast_answer_card> answers)
        {
            packData = new cardcast_pack();

            //some basic checks on packCode. 
            packData.packCode = packCode.ToUpper();
            Regex r = new Regex("^[A-Z0-9]*$");
            if (!r.IsMatch(packCode))
            {
                Roboto.log.log("Pack failed regex match and was dropped - " + packCode, logging.loglevel.high);
                return false;

            }
            else
            {
                //call the pack info API
                JObject packInfoResponse = sendPOST(packCode);
                //get the various bits we need. 
                if (packInfoResponse == null)
                {
                    Roboto.log.log("Null response from cardcast API - " + packCode, logging.loglevel.high);
                    return false;
                }
                else
                {
                    try
                    {
                        //get the message details
                        packData.name = packInfoResponse.SelectToken("name").Value<string>();
                        packData.description = packInfoResponse.SelectToken("description").Value<string>();
                    }
                    catch (Exception e)
                    {
                        Roboto.log.log("Error parsing message - " + e.ToString(), logging.loglevel.high);
                        return false;
                    }
                }

                //now get the cards
                JObject packCardsResponse = sendPOST(packCode + "/cards");
                if (packCardsResponse == null)
                {
                    Roboto.log.log("Null response from cardcast Cards API - " + packCode, logging.loglevel.high);
                    return false;
                }
                else
                {
                    try
                    {
                        //get the questions
                        foreach (JToken token in packCardsResponse.SelectTokens("calls[*]"))
                        {
                            cardcast_question_card c = new cardcast_question_card();
                            string text = "";
                            //text is broken up into chunks, reassemble. 
                            bool first = true;
                            int nrAnswers = -1;
                            foreach (JToken textPartToken in token.SelectTokens("text[*]"))
                            {
                                text += (first?"":"__") + textPartToken.Value<string>();
                                first = false;
                                nrAnswers++;
                            }

                            c.question = text;
                            c.nrAnswers = nrAnswers;
                            questions.Add(c);
                        }
                        //get the answers
                        foreach (JToken token in packCardsResponse.SelectTokens("responses[*]"))
                        {
                            cardcast_answer_card c = new cardcast_answer_card();
                            string text = "";
                            //text is broken up into chunks, reassemble. 
                            bool first = true;
                            foreach (JToken textPartToken in token.SelectTokens("text[*]"))
                            {
                                text += (first ? "" : "__") + textPartToken.Value<string>();
                                first = false;
                            }

                            c.answer = text;
                            answers.Add(c);
                        }
                        
                    }
                    catch (Exception e)
                    {
                        Roboto.log.log("Error parsing message - " + e.ToString(), logging.loglevel.high);
                        return false;
                    }
                }

                return true;
            }
        }



        //TODO - merge this with the telegramAPI and steamAPI classes, probably.

        /// <summary>
        /// Sends a POST message, returns the reply object
        /// </summary>
        /// <returns></returns>
        private static JObject sendPOST(String methodURL)
        {
            string finalString = cardCastURL + methodURL;

            //sort out encodings and clients.
            Encoding enc = Encoding.GetEncoding(1252);
            WebClient client = new WebClient();
            
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(finalString);
            request.Method = "GET";
            request.ContentType = "application/json";
            Roboto.log.log("Calling CardCast API:\n\r" + request.RequestUri.ToString(), logging.loglevel.low);
            try
            {

                HttpWebResponse webResponse = (HttpWebResponse)request.GetResponse();

                if (webResponse != null)
                {
                    StreamReader responseSR = new StreamReader(webResponse.GetResponseStream(), enc);
                    string response = responseSR.ReadToEnd();

                    JObject jo = JObject.Parse(response);

                    //success
                    //TODO - for Steam this is a "success" object that should return "true". Not the first item.
                    string path = jo.First.Path;

                    //if (path != "ok" || result != "True")
                    //{
                    //    Console.WriteLine("Error recieved sending message!");
                    //throw new WebException("Failure code from web service");

                    //}
                    Roboto.log.log("Message Success", logging.loglevel.low);
                    return jo;

                }
            }
            catch (Exception e)
            {
                Roboto.log.log("CardCast Call failed " + e.ToString(), logging.loglevel.critical);
                throw new WebException("Error during method call", e);
            }

            return null;
        }

    }
}