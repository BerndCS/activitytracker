import sqlite3
import os
import pandas as pd

def get_db_connection():
    appdata = os.getenv('APPDATA')
    db_path = os.path.join(appdata, 'TraceTime', 'activity_log.db')
    return sqlite3.connect(db_path)

def get_all_logs():
    """Holt die Rohdaten für die Listenansicht"""
    conn = get_db_connection()
    query = "SELECT Id, AppName, Action, Timestamp FROM Logs ORDER BY Timestamp DESC"
    df = pd.read_sql_query(query, conn)
    conn.close()
    return df

def get_alltime_stats():
    """Berechnet die All-Time Statistik (dein Wunsch!)"""
    conn = get_db_connection()
    df = pd.read_sql_query("SELECT * FROM Logs ORDER BY AppName, Timestamp", conn)
    conn.close()
    
    return df

if __name__ == "__main__":
    print("Rufe Logs ab...")
    logs = get_all_logs()
    print(logs.head(20)) 