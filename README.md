# HoflettMicroProcessors
Microprocessors for Logic World

This mod adds a RISC-V microprocessor (R32I), with a 32-bit interface to talk to memory. (More in the future maybe)

Put the HoflettMicroP folder in the GameData folder of the game.

## How to use
![image](https://github.com/ImMihai689/HoflettMicroProcessors/assets/75139772/948a17c8-8249-4a18-a3e8-23b83b419de8)
|    Number    |  Functionality |     Notes     |
| :---         |     :---       |:---           |
| 1            | Clock          | Things actually happen on the rising edge. |
| 2            | Reset          | Resets PC and all output pins. Does not affect register conents. |
| 3            | Read select    | Left is read word, middle is read halfword, right is read byte. |
| 4            | Write select   | Same ordering as read select. |
| 5            | Memory bus I/O | Left is LSB, right is MSB. These pins should be tied together, and should connect to other circuitry by the input pins. |
| 6            | Memory bus address | Left is LSB, right is MSB. |
| 7            | Interrupts     | Unused. |

### Behaviour
(This applies to after a reset)
On the first rising edge, PC is put on the address bus.
On the second rising edge a word is read, preferably an instruction, and is executed in that tick and PC is incremented by 4, as per the manual. The following rising edge will be the next instruction's first rising edge.

Store instructions put the data to be stored, the address to store is at, and the size of the data (Write select pin) on their respective outputs on the second falling edge of the instruction. Other that that it's like any other instruction.

Load instructions put the address to read from and the size of the data (Read select pin) on the respective outputs on the second rising edge of the instruction, and on the third one it reads the data from the input pins. The next rising edge is the next instruction's first rising edge.

