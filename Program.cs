using System;
using System.IO;
using System.Linq;

using MySql.Data.MySqlClient;
using ServiceStack.OrmLite;
using ServiceStack.Text;

using HttpServer;
using System.Configuration;
using System.Diagnostics;



namespace QueryVizualizer
{
	class Program
	{
		static void OnRequest(object sender, RequestEventArgs e)
		{
			if (e.Request.Uri.OriginalString.Contains("data.json"))
			{
				byte[] buffer = File.ReadAllBytes("data.json");
				e.Response.Body.Write(buffer, 0, buffer.Length);
			}
			else
			{
				byte[] buffer = File.ReadAllBytes("index.html");
				e.Response.Body.Write(buffer, 0, buffer.Length);
			}
		}

		static void Main(string[] args)
		{
			const int Port = 8080;


			if (ConfigurationManager.ConnectionStrings["db"] == null ||
				ConfigurationManager.ConnectionStrings["db"].ConnectionString == "")
			{
				Console.WriteLine("Please add a connection string to app.config");
				Console.ReadLine();
				return;
			}

			var connectionString = ConfigurationManager.ConnectionStrings["db"].ConnectionString;
			var factory = new OrmLiteConnectionFactory(connectionString, MySqlDialect.Provider);

			Console.WriteLine("Opening MySQL connection...");
			var conn = factory.OpenDbConnection();

			Console.WriteLine("Starting Web Listener on port " + Port);
			var http = HttpServer.HttpListener.Create(System.Net.IPAddress.Any, Port);
			http.RequestReceived += OnRequest;
			http.Start(10);


			while (true)
			{
				Console.WriteLine("Press enter to start logging");
				Console.ReadLine();

				Console.WriteLine("Clearing Log Table...");
				conn.ExecuteSql("TRUNCATE TABLE mysql.general_log;");

				var watch = Stopwatch.StartNew();
				Console.WriteLine("--- Logging, Press enter to stop---");
				conn.ExecuteSql("SET GLOBAL log_output='TABLE'");
				conn.ExecuteSql("SET GLOBAL general_log='ON'");

				Console.ReadLine();
				conn.ExecuteSql("SET GLOBAL general_log='OFF'");


				Console.WriteLine("Reading Log");

				var log = conn.Select<DatabaseRow>("SELECT * FROM mysql.general_log ORDER BY event_time LIMIT 2000");
				var jsonLog = log.Select(f => new JsonRow(f)).ToList();


				Console.WriteLine("Got " + log.Count + " events");

				Console.WriteLine("Writing Log");
				File.WriteAllText("data.json", jsonLog.ToJson());

				Console.WriteLine("Done!");
				Console.WriteLine("Open http://localhost:" + Port + " to see the queries");
				Console.WriteLine();
			}
		}
	}


	class DatabaseRow
	{
		public DateTime event_time { get; set; }
		public string user_host { get; set; }
		public int thread_id { get; set; }
		public int server_id { get; set; }
		public string command_type { get; set; }
		public string argument { get; set; }
	}

	class JsonRow
	{
		public JsonRow(DatabaseRow row)
		{
			time = row.event_time.ToUnixTimeMs();
			host = row.user_host;
			thread_id = row.thread_id;
			server_id = row.server_id;
			command_type = row.command_type;
			argument = row.argument;
		}

		public long time { get; set; }
		public string host { get; set; }
		public int thread_id { get; set; }
		public int server_id { get; set; }
		public string command_type { get; set; }
		public string argument { get; set; }
	}
}
