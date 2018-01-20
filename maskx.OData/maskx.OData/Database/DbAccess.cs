
//----------------------------------------------------------------------------------------
//https://databooster.codeplex.com/
//---------------------------------------------------------------------------------------
using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;

namespace maskx.Database
{
    class DbAccess<Tpar, TparCollection> : IDisposable
        where Tpar : DbParameter
        where TparCollection : DbParameterCollection
    {
        #region Memeber
        DbProviderFactory _ProviderFactory;
        DbConnection _Connection;
        const int _MaxRetryCount = 2;
        const int _IncreasingDelayRetry = 500;
        #endregion

        #region Construct
        public DbAccess(DbConnection dbConnection)
        {
            _Connection = dbConnection;
            _Connection.Open();
        }

        #endregion

        #region Method
        public DbParameterCollection ExecuteReader(string commandText, Action<DbDataReader> dataReader, Action<TparCollection> parametersBuilder = null, CommandType commandType = CommandType.StoredProcedure, int commandTimeout = 0)
        {
            using (DbDataReader reader = CreateReader(commandText, commandTimeout, commandType, parametersBuilder, out DbParameterCollection pars))
            {
                if (dataReader == null)
                    return pars;
                while (reader.Read())
                    dataReader(reader);
                reader.Close();
                return pars;
            }
        }
        public object ExecuteScalar(string commandText, Action<TparCollection> parametersBuilder,
           CommandType commandType = CommandType.StoredProcedure, int commandTimeout = 0)
        {
            object rtv = 0;

            for (int retry = 0; ; retry++)
            {
                try
                {
                    rtv = CreateCommand(commandText, commandTimeout, commandType, parametersBuilder).ExecuteScalar();
                    break;
                }
                catch (Exception e)
                {
                    if (retry < _MaxRetryCount && OnConnectionLost(e))
                        ReConnect(retry);
                    else
                        throw;
                }
            }

            return rtv;
        }
        public int ExecuteNonQuery(string commandText, Action<TparCollection> parametersBuilder,
            CommandType commandType = CommandType.StoredProcedure, int commandTimeout = 0)
        {
            int nAffectedRows = 0;

            for (int retry = 0; ; retry++)
            {
                try
                {
                    nAffectedRows = CreateCommand(commandText, commandTimeout, commandType, parametersBuilder).ExecuteNonQuery();
                    break;
                }
                catch (Exception e)
                {
                    if (retry < _MaxRetryCount && OnConnectionLost(e))
                        ReConnect(retry);
                    else
                        throw;
                }
            }

            return nAffectedRows;
        }
        DbDataReader CreateReader(string commandText
            , int commandTimeout
            , CommandType commandType
            , Action<TparCollection> parametersBuilder
            , out DbParameterCollection pars
            )
        {
            for (int retry = 0; ; retry++)
            {
                try
                {
                    var dbCmd = CreateCommand(commandText, commandTimeout, commandType, parametersBuilder);
                    pars = dbCmd.Parameters;
                    return dbCmd.ExecuteReader(CommandBehavior.CloseConnection);
                }
                catch (Exception e)
                {
                    if (retry < _MaxRetryCount && OnConnectionLost(e))
                        ReConnect(retry);
                    else
                        throw;
                }
            }
        }
        bool OnConnectionLost(Exception dbException)
        {
            bool canRetry = false;

            SqlException e = dbException as SqlException;

            if (e == null)
                canRetry = false;
            else
                switch (e.Number)
                {
                    case 233:
                    case -2: canRetry = true; break;
                    default: canRetry = false; break;
                }
            return canRetry;
        }
        void ReConnect(int retrying)
        {
            if (_Connection != null)
                if (_Connection.State != ConnectionState.Closed)
                {
                    _Connection.Close();

                    if (retrying > 0)
                        Thread.Sleep(retrying * _IncreasingDelayRetry);	// retrying starts at 0, increases delay time for every retry.

                    _Connection.Open();
                }
        }
        DbCommand CreateCommand(string commandText, int commandTimeout, CommandType commandType, Action<TparCollection> parametersBuilder)
        {
            if (_Connection == null)
                throw new ObjectDisposedException("DbAccess");

            var dbCommand = _Connection.CreateCommand();
            dbCommand.CommandType = commandType;
            dbCommand.CommandText = commandText;

            if (commandTimeout > 0)
                dbCommand.CommandTimeout = commandTimeout;

            parametersBuilder?.Invoke(dbCommand.Parameters as TparCollection);

            return dbCommand;
        }
        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _Connection != null)
            {
                if (_Connection.State != ConnectionState.Closed)
                {
                    _Connection.Dispose();
                }

                _Connection = null;
            }
        }
        #endregion
    }
}
