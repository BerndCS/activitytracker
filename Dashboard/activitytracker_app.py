import sys
import os
import sqlite3
import pandas as pd
from PyQt6.QtWidgets import (QApplication, QMainWindow, QTabWidget, 
                             QWidget, QVBoxLayout, QTableWidget, 
                             QTableWidgetItem, QLabel, QHeaderView)
from PyQt6.QtCore import Qt


BLACKLIST = [
    'conhost.exe', 'dllhost.exe', 'RuntimeBroker.exe', 'svchost.exe', 
    'SearchHost.exe', 'ShellExperienceHost.exe', 'taskhostw.exe',
    'wmpnetwk.exe', 'lsass.exe', 'csrss.exe', 'smss.exe', 'wininit.exe',
    'services.exe', 'winlogon.exe', 'fontdrvhost.exe', 'dwm.exe'
]

class ActivityApp(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Activitytracker Dashboard")
        self.resize(1100, 750)

        # Tabs erstellen
        self.tabs = QTabWidget()
        self.setCentralWidget(self.tabs)

        self.stats_tab = QWidget()
        self.setup_stats_tab()
        self.tabs.addTab(self.stats_tab, "Alltime Statistiken")

        self.log_tab = QWidget()
        self.setup_log_tab()
        self.tabs.addTab(self.log_tab, "Protokoll Verlauf")

        self.refresh_data()

    def get_db_path(self):
        appdata = os.getenv('APPDATA')
        return os.path.join(appdata, 'TraceTime', 'activity_log.db')

    def setup_stats_tab(self):
        layout = QVBoxLayout()
        header = QLabel("Deine Programmnutzung insgesamt")
        header.setStyleSheet("font-size: 20px; font-weight: bold; margin-bottom: 10px;")
        layout.addWidget(header)
        
        self.stats_table = QTableWidget()
        self.stats_table.setColumnCount(2)
        self.stats_table.setHorizontalHeaderLabels(["Anwendung", "Gesamtzeit (Minuten)"])
        self.stats_table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        layout.addWidget(self.stats_table)
        
        self.stats_tab.setLayout(layout)

    def setup_log_tab(self):
        layout = QVBoxLayout()
        header = QLabel("Letzte Aktivitäten")
        header.setStyleSheet("font-size: 18px; font-weight: bold;")
        layout.addWidget(header)
        
        self.table = QTableWidget()
        self.table.setColumnCount(3)
        self.table.setHorizontalHeaderLabels(["Programm", "Aktion", "Zeitpunkt (UTC)"])
        self.table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        layout.addWidget(self.table)
        self.log_tab.setLayout(layout)

    def calculate_durations(self, df):
        """Rechnet START/STOP Paare in Minuten um (wie im Dashboard)"""
        df['Timestamp'] = pd.to_datetime(df['Timestamp'])
        df = df.sort_values(['AppName', 'Timestamp'])
        
        app_durations = {}
        for app, group in df.groupby('AppName'):
            start_time = None
            total_seconds = 0
            for _, row in group.iterrows():
                if row['Action'] == 'START':
                    start_time = row['Timestamp']
                elif row['Action'] == 'STOP' and start_time is not None:
                    total_seconds += (row['Timestamp'] - start_time).total_seconds()
                    start_time = None
            
            if total_seconds > 0:
                app_durations[app] = round(total_seconds / 60, 2)
        
        return pd.Series(app_durations).sort_values(ascending=False)

    def refresh_data(self):
        db_path = self.get_db_path()
        if not os.path.exists(db_path):
            return

        conn = sqlite3.connect(db_path)
        df_raw = pd.read_sql_query("SELECT * FROM Logs ORDER BY Timestamp DESC", conn)
        conn.close()

        df_filtered = df_raw[~df_raw['AppName'].isin(BLACKLIST)]

        stats = self.calculate_durations(df_filtered)
        self.stats_table.setRowCount(len(stats))
        for i, (app, minutes) in enumerate(stats.items()):
            self.stats_table.setItem(i, 0, QTableWidgetItem(app))
            self.stats_table.setItem(i, 1, QTableWidgetItem(f"{minutes} min"))

        self.table.setRowCount(len(df_filtered))
        for i, row in df_filtered.iterrows():
            self.table.setItem(i, 0, QTableWidgetItem(str(row['AppName'])))
            self.table.setItem(i, 1, QTableWidgetItem(str(row['Action'])))
            self.table.setItem(i, 2, QTableWidgetItem(str(row['Timestamp'])))

if __name__ == "__main__":
    app = QApplication(sys.argv)
    app.setStyle("Fusion") 
    window = ActivityApp()
    window.show()
    sys.exit(app.exec())