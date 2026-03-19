import streamlit as st
import pandas as pd
import sqlite3
import os

BLACKLIST = [
    'conhost.exe', 'dllhost.exe', 'RuntimeBroker.exe', 'svchost.exe', 
    'SearchHost.exe', 'ShellExperienceHost.exe', 'taskhostw.exe',
    'wmpnetwk.exe', 'lsass.exe', 'csrss.exe', 'smss.exe', 'wininit.exe',
    'services.exe', 'winlogon.exe', 'fontdrvhost.exe', 'dwm.exe'
]

st.set_page_config(page_title="Activitytracker Dashboard", layout="wide")

def get_db_connection():
    appdata = os.getenv('APPDATA')
    db_path = os.path.join(appdata, 'TraceTime', 'activity_log.db')
    
    if not os.path.exists(db_path):
        return None
        
    return sqlite3.connect(f"file:{db_path}?mode=ro", uri=True)

st.title("Activitytracker")

tab1, tab2 = st.tabs(["Alltime Daten", "Protokoll Verlauf"])

df = pd.DataFrame()

with tab1:
    st.header("Deine Nutzung insgesamt")
    conn = get_db_connection()
    if conn is not None:
        df = pd.read_sql_query("SELECT * FROM Logs ORDER BY AppName, Timestamp", conn)
        conn.close()

        df = df[~df['AppName'].isin(BLACKLIST)]
        
        if not df.empty:
            event_counts = df['AppName'].value_counts()
            st.bar_chart(event_counts)
        else:
            st.info("Datenbank gefunden, aber noch keine Einträge vorhanden.")
    else:
        st.warning("⚠️ Datenbank noch nicht gefunden. Hast du den Collector schon gestartet?")
        st.info(f"Suche an diesem Ort: %AppData%/TraceTime/activity_log.db")

with tab2:
    st.header("Rohdaten-Log")
    conn = get_db_connection()
    
    if conn is not None:
        df_logs = pd.read_sql_query("SELECT Id, AppName, Action, Timestamp FROM Logs ORDER BY Timestamp DESC", conn)
        conn.close()

        df_logs = df_logs[~df_logs['AppName'].isin(BLACKLIST)]

        st.dataframe(df_logs, use_container_width=True)
    else:
        st.info("Log ist leer, da keine Datenbank gefunden wurde.")