#!/usr/bin/env python2
import ssl
import socket
import json
import urllib2

# -------------------------------user variables-------------------------------
server_name = ''
server_port = 24654
login = ''
# ----------------------------------------------------------------------------

ip = json.load(urllib2.urlopen('https://ipinfo.io/'))['ip']
context = ssl.create_default_context()
connection = context.wrap_socket(socket.socket(socket.AF_INET), server_hostname=server_name)
connection.connect((server_name, server_port))

connection.sendall(b'login: {}\n\n'.format(login))
print(connection.recv(1024))
connection.sendall(b'change-ip: {}\n\n'.format(ip))
print(connection.recv(1024))

connection.close()