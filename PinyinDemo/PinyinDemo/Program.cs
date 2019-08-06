using Microsoft.International.Converters.PinYinConverter;
using MySql.Data.MySqlClient;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace PinyinDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            //测试
            //Console.WriteLine(GetNamePinYin("家加价","",""));
            //Console.WriteLine(GetNamePinYin("洋"));
            //Console.WriteLine(GetNamePinYin("有"));
            //Console.WriteLine(GetNamePinYin("屹"));
            //Console.WriteLine(GetNamePinYin("育"));

            var dbHost = "server=localhost;port=3306;User Id=root;Pwd=ZZZZZZZ;Persist Security Info=True;database=duoyinzi";
            var redisConnect = "localhost:9999,defaultDatabase=1,abortConnect=false,ssl=false";
            var cachePrefix = "";
            Console.WriteLine(GetNamePinYin("单洋育", dbHost, redisConnect, cachePrefix));

            Console.ReadKey();

        }

        /// <summary>
        /// 静态资源：存放百家姓
        /// </summary>
        static Dictionary<string, string> dictionary;

        /// <summary>
        /// 静态构造函数
        /// </summary
        static Program()
        {
            dictionary = ReadPinYinConfiguration();
        }

        /// <summary>
        /// 获取姓名拼音
        /// </summary>
        /// <param name="name">姓名</param>
        /// <returns>转换后的拼音</returns>
        public static string GetNamePinYin(string name, string dbHost, string redisConnect, string cachePrefix)
        {
            //初始化字典集
            dictionary = ReadPinYinConfiguration();
            //只有一个字符
            if (name.Length == 1)
            {
                //命中百家姓单姓，直接返回单姓
                if (dictionary.ContainsKey(name))
                {
                    return dictionary[name];
                }
                return GetPinyin(name);
            }
            var namePinYin = new StringBuilder();
            var num = 0;
            //判断key是否存在
            if (dictionary.ContainsKey(name.Substring(0, 2)))
            {
                namePinYin.Append(dictionary[name.Substring(0, 2)]);
                num = 2;
            }
            else
            {
                //判断key是否存在
                if (dictionary.ContainsKey(name.Substring(0, 1)))
                {
                    namePinYin.Append(dictionary[name.Substring(0, 1)]);
                    num = 1;
                }
            }
            //名命中
            var ming = name.Remove(0, num);
            if (ming.Length > 0)
            {
                //判断dbHost和redisHost是否配置
                if (!string.IsNullOrWhiteSpace(dbHost) || !string.IsNullOrWhiteSpace(redisConnect))
                {
                    //判断多音字优先字典是否存在
                    var redisHash = GetPinyinHashByRedis(redisConnect, cachePrefix);
                    if (redisHash == null || redisHash.Count() <= 0)//不存在
                    {
                        //从db获取多音字优先字典并存入
                        SetPinyinHashToRedis(GetPinyinDictionary(dbHost), redisConnect, cachePrefix);
                        //从redis取出
                        redisHash = GetPinyinHashByRedis(redisConnect, cachePrefix);
                    }
                    //尝试命中多音字
                    foreach (var item in ming)
                    {
                        var zi = item.ToString();
                        if (redisHash.ContainsKey(zi))
                        {
                            namePinYin.Append(redisHash[zi]);
                        }
                        else
                        {
                            //常规拼音转换
                            namePinYin.Append(GetPinyin(zi));
                        }
                    }
                }
                else
                {
                    //常规拼音转换
                    namePinYin.Append(GetPinyin(ming));
                }
            }
            return namePinYin.ToString();
        }

        /// <summary>
        /// 读取拼音配置项
        /// </summary>
        /// <returns>字典集</returns>
        public static Dictionary<string, string> ReadPinYinConfiguration()
        {
            //文件路径
            var filePath = @"C:\Users\greed\source\repos\PinyinDemo\PinyinDemo\baijiaxing.txt";
            //定义字典集
            var dictionary = new Dictionary<string, string>();
            //读取文件
            if (File.Exists(filePath))
            {
                using (StreamReader sr = new StreamReader(filePath, Encoding.UTF8))
                {
                    var line = string.Empty;
                    while ((line = sr.ReadLine()) != null)
                    {
                        //处理音调
                        line = ReplaceString(line);
                        //处理字典集
                        var key = new StringBuilder();
                        var value = new StringBuilder();
                        foreach (var item in line)
                        {
                            if (item <= 128)
                            {
                                //拼音，加入value
                                if (!char.IsWhiteSpace(item))
                                {
                                    value.Append(item);
                                }
                            }
                            else
                            {
                                //汉字，加入key
                                key.Append(item);
                            }
                        }
                        dictionary.Add(key.ToString(), value.ToString());
                    }
                }
            }
            return dictionary;
        }

        /// <summary>
        /// 替换声调
        /// </summary>
        /// <param name="str">字符串</param>
        /// <returns>处理后的字符串</returns>
        public static string ReplaceString(string str)
        {
            //a
            str = str.Replace("ā", "a");
            str = str.Replace("á", "a");
            str = str.Replace("ǎ", "a");
            str = str.Replace("à", "a");
            //e
            str = str.Replace("ē", "e");
            str = str.Replace("é", "e");
            str = str.Replace("ě", "e");
            str = str.Replace("è", "e");
            //i
            str = str.Replace("ī", "i");
            str = str.Replace("í", "i");
            str = str.Replace("ǐ", "i");
            str = str.Replace("ì", "i");
            //o
            str = str.Replace("ō", "o");
            str = str.Replace("ó", "o");
            str = str.Replace("ǒ", "o");
            str = str.Replace("ò", "o");
            //u
            str = str.Replace("ū", "u");
            str = str.Replace("ú", "u");
            str = str.Replace("ǔ", "u");
            str = str.Replace("ù", "u");
            //v
            str = str.Replace("ǖ", "v");
            str = str.Replace("ǘ", "v");
            str = str.Replace("ǚ", "v");
            str = str.Replace("ǜ", "v");
            //去除空格
            str = str.Replace(" ", string.Empty);
            return str;
        }

        /// <summary>   
        /// 汉字转化为拼音  
        /// </summary>   
        /// <param name="str">汉字</param>   
        /// <returns>全拼</returns>   
        public static string GetPinyin(string str)
        {
            string r = string.Empty;
            foreach (char obj in str)
            {
                try
                {
                    ChineseChar chineseChar = new ChineseChar(obj);
                    string t = chineseChar.Pinyins[0].ToString();
                    r += t.Substring(0, t.Length - 1);
                }
                catch
                {
                    r += obj.ToString();
                }
            }
            return r.ToLower();
        }

        /// <summary>
        /// 获取拼音字典
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetPinyinDictionary(string connectionString)
        {
            
            var conn = new MySqlConnection(connectionString);
            try
            {
                conn.Open();
                using (var command = conn.CreateCommand())
                {
                    var result = new Dictionary<string, string>();
                    command.CommandText = "SELECT WordKey,WordValue FROM duoyinzidictionary;";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var key = reader["WordKey"].ToString();
                            var value = reader["WordValue"].ToString();
                            result.Add(key, value);
                        }
                    }
                    return result;
                }
            }
            finally
            {
                conn.Close();
            }

        }

        /// <summary>
        /// 设置拼音hash到redis
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="host"></param>
        public static void SetPinyinHashToRedis(Dictionary<string, string> dictionary, string redisConnect, string cachePrefix)
        {
            try
            {
                ////构造一个redis实例
                //var redis = new RedisCache(redisConnect, cachePrefix);
                ////存入多音字字典，过期时间为60s
                //redis.Set("pinyinset", dictionary, DateTime.Now.AddSeconds(60));

                using (ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnect))
                {
                    IDatabase db = redis.GetDatabase();
                    //此处应该转成json存储一条redis
                    foreach (var item in dictionary)
                    {
                        RedisValue name = item.Key;
                        RedisValue value = item.Value;
                        var hash = new HashEntry(name, value);
                        db.HashSet("pinyinset", new HashEntry[] { hash });
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        
        /// <summary>
        /// 从redis获取拼音hash
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetPinyinHashByRedis(string redisConnect, string cachePrefix)
        {
            try
            {
                ////构造一个redis实例
                //var redis = new RedisCache(redisConnect, cachePrefix);
                ////获取多音字字典
                //var pinyinset = redis.Get<Dictionary<string, string>>("pinyinset");
                //return pinyinset;

                using (ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnect))
                {
                    IDatabase db = redis.GetDatabase();
                    var hashset = db.HashGetAll("pinyinset");
                    var result = new Dictionary<string, string>();
                    if (hashset.Count() > 0)
                    {
                        foreach (var item in hashset)
                        {
                            var key = item.Name;
                            var value = item.Value;
                            result.Add(key, value);
                        }
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 清除redis缓存以支持热更新
        /// </summary>
        /// <param name="redisConnect"></param>
        /// <param name="cachePrefix"></param>
        /// <returns></returns>
        public static bool RemoveRedis(string redisConnect, string cachePrefix)
        {
            ////构造一个redis实例
            //var redis = new RedisCache(redisConnect, cachePrefix);
            ////Remove pinyinset
            //redis.Remove("pinyinset");
            return true;
        }

        /// <summary>
        /// 增加多音字优先命中到数据库
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="pinyinDic"></param>
        /// <returns></returns>
        public static bool AddPinyin(string connectionString, string key,string value)
        {
            var conn = new MySqlConnection(connectionString);
            try
            {
                conn.Open();
                using (var command = conn.CreateCommand())
                {
                    command.CommandText = $"INSERT INTO duoyinzidictionary(WordKey,WordValue) VALUES({key},{value})";
                    var result = command.ExecuteNonQuery();
                    return result > 0;
                }
            }
            finally
            {
                conn.Close();
            }
        }
    }
}
