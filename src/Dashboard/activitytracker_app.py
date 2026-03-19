import sys
import os
import sqlite3
import subprocess
import psutil
import pandas as pd
from datetime import datetime, timezone
from PyQt6.QtWidgets import (QApplication, QMainWindow, QTabWidget, 
                             QWidget, QVBoxLayout, QTableWidget, 
                             QTableWidgetItem, QLabel, QHeaderView,
                             QSystemTrayIcon, QMenu)
from PyQt6.QtCore import Qt, QTimer
from PyQt6.QtGui import QIcon, QAction


BLACKLIST = [
    'conhost.exe', 'dllhost.exe', 'RuntimeBroker.exe', 'svchost.exe',
    'SearchHost.exe', 'ShellExperienceHost.exe', 'taskhostw.exe',
    'wmpnetwk.exe', 'lsass.exe', 'csrss.exe', 'smss.exe', 'wininit.exe',
    'services.exe', 'winlogon.exe', 'fontdrvhost.exe', 'dwm.exe',
    # Eigene Prozesse ausblenden
    'python.exe', 'pythonw.exe', 'activitytracker_app.exe', 'TraceTimeCollector.exe'
]

class ActivityApp(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Activitytracker Dashboard")
        self.resize(1100, 750)

        self.tabs = QTabWidget()
        self.setCentralWidget(self.tabs)

        self.stats_tab = QWidget()
        self.setup_stats_tab()
        self.tabs.addTab(self.stats_tab, "Alltime Statistiken")

        self.log_tab = QWidget()
        self.setup_log_tab()
        self.tabs.addTab(self.log_tab, "Protokoll Verlauf")

        self.setup_system_tray()

        self.update_timer = QTimer()
        self.update_timer.timeout.connect(self.refresh_data)
        self.update_timer.start(500)

        self.refresh_data()

    def get_db_path(self):
        appdata = os.getenv('APPDATA')
        return os.path.join(appdata, 'TraceTime', 'activity_log.db')

    def setup_system_tray(self):
        self.tray_icon = QSystemTrayIcon(self)
        
        self.tray_icon.setIcon(self.style().standardIcon(self.style().StandardPixmap.SP_ComputerIcon))

        self.tray_icon.setToolTip("ActivityTracker")
        
        self.tray_menu = QMenu()
        
        show_action = QAction("Anzeigen", self)
        show_action.triggered.connect(self.show_from_tray)
        self.tray_menu.addAction(show_action)
        
        self.tray_menu.addSeparator()
        
        exit_action = QAction("Beenden", self)
        exit_action.triggered.connect(self.quit_application)
        self.tray_menu.addAction(exit_action)
        
        self.tray_icon.setContextMenu(self.tray_menu)
        self.tray_icon.show()
    
    def show_from_tray(self):
        self.show()
        self.raise_()
        self.activateWindow()
    
    def quit_application(self):
        """Beendet Dashboard und Collector-Prozess."""
        self.stop_collector()
        QApplication.quit()
    
    def stop_collector(self):
        """Sucht und beendet den TraceTimeCollector-Prozess."""
        try:
            for proc in psutil.process_iter(['pid', 'name']):
                try:
                    if proc.info['name'].lower() == 'tracetimecollector.exe':
                        proc.terminate()
                        proc.wait(timeout=3) 
                except (psutil.NoSuchProcess, psutil.AccessDenied, psutil.TimeoutExpired):
                    pass
        except Exception:
            pass 
    
    def closeEvent(self, event):
        event.ignore()
        self.hide()

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
        """Rechnet START/STOP Paare in Minuten um.
        Offene Sessions (START ohne STOP) werden bis jetzt gezählt.
        Sessions kürzer als 1 Sekunde werden als Messartefakte ignoriert.
        """
        df['Timestamp'] = pd.to_datetime(df['Timestamp'], utc=True)
        df = df.sort_values(['AppName', 'Timestamp'])
        now = datetime.now(timezone.utc)

        app_durations = {}
        for app, group in df.groupby('AppName'):
            start_time = None
            total_seconds = 0
            for _, row in group.iterrows():
                if row['Action'] == 'START':
                    start_time = row['Timestamp']
                elif row['Action'] == 'STOP' and start_time is not None:
                    delta = (row['Timestamp'] - start_time).total_seconds()
                    if delta >= 1:  # Messartefakte unter 1 Sekunde ignorieren
                        total_seconds += delta
                    start_time = None

            # Offene Session (läuft noch): bis jetzt zählen
            if start_time is not None:
                delta = (now - start_time).total_seconds()
                if delta >= 1:
                    total_seconds += delta

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
    
    if "--minimized" in sys.argv:
        window.hide()
    else:
        window.show()
    
    sys.exit(app.exec())