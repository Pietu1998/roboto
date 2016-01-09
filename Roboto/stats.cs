﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;
using System.IO;

namespace Roboto
{

    /// <summary>
    /// inheritable. Should be created by the plugin.
    /// </summary>
    public class statType
    {
        public string name = "";
        public string moduleType = "";
        public stats.displaymode mode = stats.displaymode.line;
        public List<statSlice> statSlices = new List<statSlice>();
        Color c = Color.Blue;

        internal statType() { }
        public statType(string name, string moduleType, Color c, stats.displaymode mode = stats.displaymode.line)
        {
            this.name = name;
            this.moduleType = moduleType;
            this.mode = mode;
        }

        public void updateDisplaySettings(Color c, stats.displaymode mode = stats.displaymode.line)
        {
            this.c = c;
            this.mode = mode;
        }

        public statSlice getSlice()
        {
            return getSlice(DateTime.Now);
        }

        public statSlice getSlice(DateTime time)
        {
            List<statSlice> matches = statSlices.Where(x => time > x.timeSlice && time < x.timeSlice.Add (stats.granularity)).ToList();
            if (matches.Count == 0)
            {
                statSlice s = new statSlice(time);
                statSlices.Add(s);
                return s;
            }
            else if (matches.Count == 1)
            {
                return matches[0];
            }
            else
            {
                Roboto.log.log("More than one match for timeslice!", logging.loglevel.high);
                return matches[0];
            }
        }

        public void logStat(statItem item)
        {
            statSlice slice = getSlice();
            slice.addCount(item.items);
        }

        /// <summary>
        /// Generate a series of datapoints for use on a graph
        /// </summary>
        /// <param name="startTime"></param>
        /// <returns></returns>
        public Series getSeries(DateTime startTime)
        {
            TimeSpan startTSAgo = TimeSpan.FromTicks( stats.granularity.Ticks * stats.graphYAxisCount);
            Series s = new Series(this.moduleType + ">" + this.name);
            s.Color = c;

            if (mode == stats.displaymode.line)
            {
                s.ChartType = SeriesChartType.Line;
            }
            for (int i = 0; i < stats.graphYAxisCount; i++)
            {
                DateTime point = startTime.Subtract(TimeSpan.FromTicks(stats.granularity.Ticks * i));
                statSlice slice = getSlice(point);
                if (slice != null)
                {
                    DataPoint p = new DataPoint(point.Subtract(startTime).TotalMinutes, slice.count);
                    s.Points.Add(p);
                }

            }

            return s;
        }

        public void removeOldData()
        {
            DateTime cutoff = DateTime.Now.Subtract(new TimeSpan(stats.granularity.Ticks * stats.graphYAxisCount));
            statSlices.RemoveAll(x => x.timeSlice < cutoff);
        }
    }

    public class statSlice
    {
        public DateTime timeSlice = DateTime.MinValue;
        public int count = 0;
        internal statSlice() { }
        public statSlice(DateTime timeSlice)
        {
            //round the time down to the nearest x mins. 
            var delta = timeSlice.Ticks % stats.granularity.Ticks;
            this.timeSlice = new DateTime(timeSlice.Ticks - delta, timeSlice.Kind);
        }
        public void addCount(int items)
        {
            count += items;
        }
    }

    //an incoming item
    public class statItem
    {
        public string statTypeName;
        public string moduleType;
        public int items;
        public statItem (string statTypeName, Type moduleType, int items = 1)
        {
            this.statTypeName = statTypeName;
            this.moduleType = moduleType.ToString();
            this.items = items;
        }
    }

    /// <summary>
    /// Stats DB that is attached to settings. Used to store all incoming stats, and generate images. 
    /// </summary>
    public class stats
    {
        //constants
        public static TimeSpan granularity = new TimeSpan(0, 5, 0); //15 mins
        public static int graphYAxisCount = 100;
        public enum displaymode { line, bar };

        //data
        public List<statType> statsList = new List<statType>();

        /// <summary>
        /// Called during system startup. Adds some default types, and registers a "startup" event
        /// </summary>
        public void startup()
        {
            registerStatType("Startup", typeof(Roboto), Color.LawnGreen, displaymode.bar);
            registerStatType("Incoming Msgs", typeof(TelegramAPI), Color.Blue );
            registerStatType("Outgoing Msgs", typeof(TelegramAPI), Color.Purple);

            logStat(new statItem("Startup", typeof(Roboto)));
        }


        public void registerStatType(string name, Type moduleType, Color c, stats.displaymode mode = stats.displaymode.line)
        {
            statType existing = getStatType(name, moduleType.ToString());
            if (existing != null)
            {
                Roboto.log.log("StatType " + name + " from " + moduleType.ToString() + " already exists.", logging.loglevel.normal);
                existing.updateDisplaySettings(c, mode);
            }
            else
            {
                statType newST = new statType(name, moduleType.ToString(), c, mode);
                statsList.Add(newST);
            }

        }

        public void logStat(statItem item)
        {
            statType type = getStatType(item.statTypeName, item.moduleType.ToString());
            if (type != null)
            {
                type.logStat(item);
            }
            else
            {
                Roboto.log.log("Tried to log stat " + item.statTypeName + " for " + item.moduleType + " but doesnt exist!", logging.loglevel.high);
            }
        }

        private statType getStatType(string name, string moduleType)
        {
            List<statType> matches = statsList.Where(x => x.name == name && x.moduleType == moduleType.ToString()).ToList();
            if (matches.Count == 1 ) { return matches[0]; }
            else if (matches.Count > 1 )
            {
                Roboto.log.log("More than one match for stat " + name + " in " + moduleType, logging.loglevel.high);
                return matches[0];
            }
            else
            {
                return null;
            }

        }

        /// <summary>
        /// get all stats matching a pattern
        /// </summary>
        /// <param name="regex"></param>
        /// <returns></returns>
        private List<statType> getStatTypes (string regex)
        {
            List<statType> matches = new List<statType>();
            Regex r = new Regex(regex);
            foreach (statType t in statsList)
            {
                try
                { 
                    Match m = r.Match(t.moduleType + ">" + t.name);
                    if (m.Success)
                    {
                        matches.Add(t);
                    }
                }
                catch (Exception e)
                {
                    //will probably get some regex errors here - ignore them. 

                }
            }
            return matches;
        }


        /// <summary>
        /// Expecting a list of series names, which are the type and name, split with an ">", or can be a list of regex's
        /// </summary>
        /// <param name="series"></param>
        public Stream generateImage(List<string> series)
        {
            DateTime graphStartTime = DateTime.Now;
            
            //set up a windows form graph thing
            try
            {
                using (var ch = new Chart())
                {
                    ch.Width = 1200;
                    ch.Height = 600;

                    ChartArea cha = new ChartArea("cha");

                    cha.AxisX.Title = "Mins Ago";
                    cha.AxisX.MajorGrid.Interval = 60;
                    cha.AxisY.Title = "Value";

                    Legend l = new Legend("Legend");
                    l.DockedToChartArea = "cha";
                    l.IsDockedInsideChartArea = true;
                    l.Docking = Docking.Right;
                    
                    ch.ChartAreas.Add(cha);
                    ch.Legends.Add(l);
                    
                    Title t = new Title(Roboto.Settings.botUserName + " Statistics", Docking.Top, new System.Drawing.Font("Verdana", 13, System.Drawing.FontStyle.Bold), System.Drawing.Color.DarkGray);
                    ch.Titles.Add(t);
                    ch.TextAntiAliasingQuality = TextAntiAliasingQuality.High;

                   //if nothing passed in, assume all stats
                    if (series.Count == 0) { series.Add(".*"); }

                    //gather all matching statTypes
                    List<statType> matches = new List<statType>();

                    foreach (string s in series)
                    {
                        //populate list of statTypes that match our query. Dont worry about order / dupes - will be ordered later
                        //try exact matches
                        string[] titles = s.Trim().Split(">"[0]);
                        if (titles.Length == 2)
                        {
                            //get the series info
                            statType seriesStats = getStatType(titles[1], titles[0]);
                            if (seriesStats != null) { matches.Add(seriesStats); }
                        }

                        //try regex matches
                        List<statType> matchingTypes = getStatTypes(s);
                        foreach (statType mt in matchingTypes)
                        {
                            matches.Add(mt);
                        }
                        
                    }
                    if (matches.Count == 0)
                    {
                        Roboto.log.log("No chart type matches", logging.loglevel.warn);
                        return null;
                    }
                    else
                    {
                        matches = matches.Distinct().OrderBy(x => x.moduleType + ">" + x.name).ToList();
                        foreach (statType seriesStats in matches)
                        {
                            ch.Series.Add(seriesStats.getSeries(graphStartTime));
                        }
                        //ch.SaveImage(@"C:\temp\chart.jpg", ChartImageFormat.Jpeg);
                        MemoryStream ms = new MemoryStream();
                        ch.SaveImage(ms, ChartImageFormat.Jpeg);
                        return (ms);
                    }
                }
            }
            catch (Exception e)
            {
                Roboto.log.log("Error generating chart. " + e.ToString() , logging.loglevel.critical);
            }
            return null;
        }

        public void houseKeeping()
        {
            //Ditch any stats
            foreach (statType s in statsList)
            {
                s.removeOldData();
            }
            
            //rotate logfiles
            //TODO
        }


        //TODO Housekeeping

    }
}
