A.P 7/9/2012
Avishay.Pinsky@gmail.com

1. Installing on a new machine:
You'll need .Net framework 4.0, Speech Recognition runtime + sdk 11 - all downloadable from ms.

2. If you like to use protocol buffers for the UDP messaging (you probably don't), the QT project needs to use Windows SDK 7.1 toolchain (it's in project settings), and to staticly link to libprotobuf.lib.
Just copy the settings from the existing QTest project and you'll be fine.

3. The non-protobuf contract of the udp message:
	
	byte 0:		message length in bytes (including this one)
        byte 1:		culture code (0 = en-US, 1 = fr-CA)
        byte 2:		message confidence (0-100)
        byte 3:		opcode (for grammar-to-opcode translation, see end of this file)
        byte 4:		arg1 (optional)
        byte 5:		arg2 (optional)
        byte 6:		checksum

	examples (bytes left-to-right order):

	what user said: "shutter close" 
	recognition output: "CL" [0.9432]
	udp message:
	| 5 | 0 | 94 | 20 | 137 |

	what user said: "shutter cover three and seven" 
	recognition output: "CV 3 7" [0.874]
	udp message:
	| 7 | 0 | 87 | 40 | 3 | 7 | 112 |

4. Brief overview of configuration file (App.cfg). 

// the first settings define the udp socket binding:
  <IP>127.0.0.1</IP>	
  <port>8000</port>

// the default culture defines which SpeechRecognizer will be active on startup:
  <DefaultCulture>en-US</DefaultCulture>

// each supported culture has a matching grammar file, to be loaded to the recognizer on startup:
  <GrammarFiles>
    <string>en-US=C:\\Work\\SpeechPort\\RecognizerApp\\EnglishCmd.grxml</string>
    <string>en-GB=C:\\Work\\SpeechPort\\RecognizerApp\\test.grxml</string>
  </GrammarFiles>

// confidence level of grammar's root rule ('shutter') - recognized speech results with lower confidence on the root are ignored:
  <RootConfidenceLevel>90</RootConfidenceLevel>

// opcodes contract:
  <Opcodes>
    <Node>
      <OP>OP</OP>
      <NUM>10</NUM>
    </Node>
    <Node>
      <OP>CL</OP>
      <NUM>20</NUM>
    </Node>
    <Node>
      <OP>MV</OP>
      <NUM>30</NUM>
    </Node>
    <Node>
      <OP>CV</OP>
      <NUM>40</NUM>
    </Node>
    <Node>
      <OP>RS</OP>
      <NUM>50</NUM>
    </Node>
    <Node>
      <OP>SH</OP>
      <NUM>60</NUM>
    </Node>
  </Opcodes>
</Config>