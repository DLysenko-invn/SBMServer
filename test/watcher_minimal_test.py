import sys
sys.path.insert(0,'..')

from BLE2TCP import BLE2TCP,Packet
import time



HOST = '127.0.0.1'
PORT = 65432

WAIT_SEC = 60
CONNECT_WAIT_SEC = 15





print("Connecting to the server...")
server = BLE2TCP(HOST, PORT)
rc = server.WaitTillConnected(CONNECT_WAIT_SEC)
if (not rc):
    print("Connection failed")
    exit()
print("Connection to the server OK.")
print("")


server.RunWatcher(True)



print("Wait %i seconds..." % WAIT_SEC)
time.sleep(WAIT_SEC)
print("")

print("---")
devlist = server.GetWatcherList()["devices"]
print(devlist)

print("---")

print("Closing...")
server.Close()

