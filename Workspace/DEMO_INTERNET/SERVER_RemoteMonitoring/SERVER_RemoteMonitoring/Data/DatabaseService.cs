﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SQLite;
using SERVER_RemoteMonitoring.Services;
using SERVER_RemoteMonitoring.Models;

namespace SERVER_RemoteMonitoring.Data
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _database;

        public DatabaseService(string dbPath)
        {
            _database = new SQLiteAsyncConnection(dbPath);
        }

        public async Task InitDatabase()
        {
            var result = await _database.CreateTableAsync<Models.User>();
            var resultLog = await _database.CreateTableAsync<Models.Log>();
            var resultConnections = await _database.CreateTableAsync<Models.Connections>();
            var resultUserControl = await _database.CreateTableAsync<Models.UserLogin>();


            //await _database.CreateTableAsync<Models.Log>();
        }

        public SQLiteAsyncConnection GetDataBaseConnection() => _database;

        public async Task EnsureRoomClientTableAsync()
        {
            var db = GetDataBaseConnection();
            await db.CreateTableAsync<Models.RoomClient>();
        }
    }
}
