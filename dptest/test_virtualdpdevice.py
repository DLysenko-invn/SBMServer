import threading
import serial


from DynamicProtocol_Simulation import FakeNewportSerial
from DynamicProtocol import ISDPIOSerial,ISDPTransport,ISDPFrame,ISDPCmd



#PORT = 'COM6'
PORT = 'COM13'
BAUD = ISDPTransport.DEFAULT_BAUDRATE
TIMEOUT = 1



debugprint_mutex = threading.Lock()


def FatalError(errormessage:str):
    print("ERROR",errormessage)
    
def DebugPrintBytes(prefix:str,bytes:bytearray):
    debugprint_mutex.acquire()
    text = prefix + ''.join(format(x, '02X') for x in bytes)+' ('+str(len(bytes))+')\r'
    print(text, flush=True, sep='' )
    debugprint_mutex.release()


def port_read():

    print("Port write started...")
    
    currframe=None

    while(True):
        if (currframe==None):
            currframe = ISDPFrame()

        try:
            data = com.read(currframe.MaxRead)
        except Exception as e:
            FatalError(str(e))
            break

        if (data==None) or (len(data)==0):
            continue

        #DebugPrintBytes("R: ",data)
        bytesconsumed = currframe.Put(data)
    
        #todo: cut tail and Put it again
        assert bytesconsumed==len(data)

        if (currframe.IsError):
            FatalError(str(e))
            break


        if (currframe.IsFinished):
            cmd = ISDPCmd(currframe)
            print("R:",cmd.ToString())
            dev.ProcessFrame(currframe)
            currframe = None

    
    

def port_write():

    print("Port read started...")

    currframe=None

    while(True):

        if (currframe==None):
            currframe = ISDPFrame()
            
        data = dev.Read(currframe.MaxRead)

        if (len(data)==0):
            continue


        #DebugPrintBytes("W: ",data)

        try:
            com.write(data)
        except Exception as e:
            FatalError(str(e))
            break

        bytesconsumed = currframe.Put(data)
    
        if (currframe.IsError):
            FatalError(str(e))
            break

        if (currframe.IsFinished):
            cmd = ISDPCmd(currframe)
            print("W:",cmd.ToString())
            currframe = None
    
    



print("Virtual device @ "+PORT)

com = serial.Serial(PORT,BAUD, timeout=TIMEOUT)



dev = FakeNewportSerial()

dev.Open(None,None,TIMEOUT)



thread_read = threading.Thread(target=port_read)
thread_read.start()

thread_write = threading.Thread(target=port_write)
thread_write.start()


thread_read.join()
thread_write.join()

dev.Close()
com.close()


print("DONE")