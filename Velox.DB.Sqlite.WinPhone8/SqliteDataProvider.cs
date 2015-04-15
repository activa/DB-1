﻿#region License
//=============================================================================
// Velox.DB - Portable .NET ORM 
//
// Copyright (c) 2015 Philippe Leybaert
//
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal 
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//=============================================================================
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Velox.DB.Core;



namespace Velox.DB.Sql.Sqlite
{
    public class SqliteDataProvider : SqlDataProvider<SqliteDialect>
    {
        private readonly string _longTextType = "TEXT";

        public SqliteDataProvider(string fileName)
        {
        }

        public override int ExecuteSql(string sql, QueryParameterCollection parameters)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<SerializedEntity> Query(string sql, QueryParameterCollection parameters)
        {
            throw new NotImplementedException();
        }

        public override object QueryScalar(string sql, QueryParameterCollection parameters)
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<Dictionary<string, object>> ExecuteSqlReader(string sql, QueryParameterCollection parameters)
        {
            throw new NotImplementedException();
        }
    }
}