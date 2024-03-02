using LogicAPI.Server.Components;

namespace HoflettMicroP
{
    public class R32IH : LogicComponent
    {
        
        // Opcodes, can be found at https://riscv.org/wp-content/uploads/2017/05/riscv-spec-v2.2.pdf at Chapter 19 (page 104)

        
        const int RonRop =   0b0110011; // Register - Register opcode
        const int IonRop =   0b0010011; // Register - Immediate opcode
        const int Loadop =   0b0000011; // Load opcode
        const int Storeop =  0b0100011; // Store opcode
        const int Branchop = 0b1100011; // Branch opcode
        const int JALop =    0b1101111; // JAL opcode (JAL, JALR, LUI and AUIPC don't have space for a func3 or func7 in the encoding, unlike the other opcodes, so they need to have different opcodes)
        const int JALRop =   0b1100111; // JALR opcode
        const int LUI =      0b0110111; // LUI opcode
        const int AUIPC =    0b0010111; // AUIPC opcode

        readonly int[] MemoryInputs = {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31};
        readonly int[] MemoryOutputs = {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31};
        readonly int[] MemoryAddressOuts = {32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63};

        const int ClockPin = 32; // Clock pin
        const int ResetPin = 33; // You know what this is
        const int InterPin0 = 34; 
        const int InterPin1 = 35;
        const int InterPin2 = 36;
        const int InterPin3 = 37; // Interrupt pins, left most interrupt is InterPin0
        // These are inputs
        // interrupt pins are unused

        const int RB = 64, RH = 65, RW = 66, WB = 67, WH = 68, WW = 69; //nice
        // Read byte/half/word and Write byte/half/word, outputs

        protected bool lastClock;

        protected long[] registerBlock = new long[32];
        protected long PC = 0;
        protected long opcode, func3, func7, rd, rs1, rs2, immediate; // these values get set by ReadInstruction()


        protected int instructionPhase = 0;
        /*
            Instruction phases:
            0 - Set PC to memory address pins on rising edge
            1 - Fetch instruction from memory bus, next rising edge after phase 0, and calculate as much as possible. If the instruction is done, instructionPhase is reset, otherwise
                instrucionPhase is set to do the next step in the specific instruction
            2 - waiting to get data from memory bus

        */
        
        protected override void DoLogicUpdate()
        {
            registerBlock[0] = 0; // register x0 is always 0

            bool clock = Inputs[ClockPin].On;
            bool reset = Inputs[ResetPin].On;

            if(reset){
                PC = 0;
                SetMemoryAddressPins(0);
                SetMemoryDataPins(0);
                Outputs[RB].On = false;
                Outputs[RH].On = false;
                Outputs[RW].On = false;
                Outputs[WB].On = false;
                Outputs[WH].On = false;
                Outputs[WW].On = false;
                instructionPhase = 0;
                return;
            }

            if(lastClock == clock) return; lastClock = clock; // Kinda weird, but that's how it should work


            //separate program flow if it's executing on rising or falling edge
            if(clock)
            {
                //rising edge
                if(instructionPhase == 0) SetPCToOutput();
                else if(instructionPhase == 1) ReadInstruction();
                else if(instructionPhase == 2) Loadpart2();
            }
            else
            {
                //falling edge
            }

        }

        
        protected void SetPCToOutput(){
            Outputs[RB].On = false;
            Outputs[RH].On = false;
            Outputs[RW].On = true; // we do want this
            Outputs[WB].On = false;
            Outputs[WH].On = false;
            Outputs[WW].On = false;
            // just to be sure none of them are active
            SetMemoryAddressPins(PC); // put those bits out into the world
            SetMemoryDataPins(0); // Clear those pins just to be surew
            instructionPhase = 1; // increment phase
        }

        protected void ResetInstructionPhase(long increment){
            PC += increment; // Memory is byte addressable, but instructions are 4 bytes!
            instructionPhase = 0;
        }

        protected void ReadInstruction(){
            long instruction = GetDataFromMemoryPins();
            Outputs[RW].On = false; // we're done with that

            opcode = (instruction &            0b00000000000000000000000001111111) >> 0; // separate the first 7 bits of the instruction
            rd = (instruction &                0b00000000000000000000111110000000) >> 7; // and so on, refer to the RISC-V manual
            func3 = (instruction &             0b00000000000000000111000000000000) >> 12;
            rs1 = (instruction &               0b00000000000011111000000000000000) >> 15;
            rs2 = (instruction &               0b00000001111100000000000000000000) >> 20;
            func7 = (instruction &             0b11111110000000000000000000000000) >> 25;
            // doesn't matter if some get useless values in them because they won't be used

            // get immediate for each opcode type (exept register register) and do that instruction
            if(opcode == IonRop || opcode == Loadop || opcode == JALRop){ 
                immediate = (long)((int)((instruction & 0b11111111111100000000000000000000) >> 20));
                if(opcode == IonRop)
                    RegisterImmediateInstruction();
                else if (opcode == Loadop)
                    Loadpart1();
                else
                    JALRfunc();
            } else
            if(opcode == Storeop){ 
                immediate = (long)((int)(((instruction & 0b11111110000000000000000000000000) >> 20) | ((instruction & 0b00000000000000000000111110000000) >> 7)));
                Store();
            } else
            if(opcode == Branchop) {
                immediate  = (instruction &      0b00000000000000000000111100000000) >> 7;
                immediate |= (instruction &      0b01111110000000000000000000000000) >> 20;
                immediate |= (instruction &                  0b00000000000000000000000010000000)   << 4;
                immediate |= (long)((int)((instruction & 0b10000000000000000000000000000000) >> 19));
                BranchInstruction();
            } else
            if(opcode == JALop){
                immediate  = (instruction &    0b01111111111000000000000000000000) >> 20;
                immediate |= (instruction &    0b00000000000100000000000000000000) >> 9;
                immediate |= (instruction &    0b00000000000011111111000000000000);
                immediate |= (long)((int)((instruction &    0b10000000000000000000000000000000) >> 11));
                JALfunc();
            } else
            if(opcode == LUI || opcode == AUIPC){
                immediate = (long)((int)(instruction & 0b11111111111111111111000000000000)); // too easy :P
                if(opcode == LUI)
                    LUIfunc();
                else
                    AUIPCfunc();
            } else
            if(opcode == RonRop){
                immediate = 0;
                RegisterRegisterInstruction();
            } else
            if(opcode == 0x00){
                ResetInstructionPhase(4);
            }

        }


        protected void Loadpart1(){
            SetMemoryAddressPins(registerBlock[rs1] + immediate);
            switch(func3){
                case 0x0:
                    Outputs[RB].On = true;
                break;
                case 0x1:
                    Outputs[RH].On = true;
                break;
                case 0x2:
                    Outputs[RW].On = true;
                break;
                case 0x4:
                    Outputs[RB].On = true;
                break;
                case 0x5:
                    Outputs[RH].On = true;
                break;
            }
            instructionPhase = 2;
        }
        protected void Loadpart2(){
            long value = GetDataFromMemoryPins();
            if(func3 == 0x0){
                value = (long)((sbyte)value);
            } else
            if(func3 == 0x1){
                value = (long)((short)value);
            }
            registerBlock[rd] = value;
            ResetInstructionPhase(4);
        }
        protected void Store(){
            SetMemoryAddressPins(registerBlock[rs1] + immediate);
            SetMemoryDataPins(registerBlock[rs2]);
            switch(func3){
                case 0x0:
                    Outputs[WB].On = true;
                break;
                case 0x1:
                    Outputs[WH].On = true;
                break;
                case 0x2:
                    Outputs[WW].On = true;
                break;
            }
            ResetInstructionPhase(4);
        }
        protected void LUIfunc(){
            registerBlock[rd] = immediate;
            ResetInstructionPhase(4);
        }
        protected void AUIPCfunc(){
            registerBlock[rd] = PC + immediate;
            ResetInstructionPhase(4);
        }
        protected void JALfunc(){
            registerBlock[rd] = PC + 4;
            ResetInstructionPhase(immediate);
        }
        protected void JALRfunc(){
            registerBlock[rd] = PC + 4;
            long newPC = ((registerBlock[rs1] + immediate) & (-2)); // Sets LSB to 0
            PC = newPC;
            instructionPhase = 0x0;
        }
        protected void RegisterRegisterInstruction(){
            if(func3 == 0x0){ // ADD / SUB
                if(func7 == 0x20)
                    registerBlock[rd] = registerBlock[rs1] - registerBlock[rs2];
                else if(func7 == 0x00)
                    registerBlock[rd] = registerBlock[rs1] + registerBlock[rs2];
            } else
            if(func3 == 0x1){ // SLL
                registerBlock[rd] = registerBlock[rs1] << (int)registerBlock[rs2];
            } else
            if(func3 == 0x5){ // SRL / SRA
                if(func7 == 0x20)
                    registerBlock[rd] = registerBlock[rs1] >> (int)registerBlock[rs2];
                else if(func7 == 0x00)
                    registerBlock[rd] = registerBlock[rs1] >> (int)registerBlock[rs2];
            } else
            if(func3 == 0x6){ // OR
                registerBlock[rd] = registerBlock[rs1] & registerBlock[rs2];
            } else
            if(func3 == 0x7){ // AND
                registerBlock[rd] = registerBlock[rs1] | registerBlock[rs2];
            } else
            if(func3 == 0x4){ // XOR
                registerBlock[rd] = registerBlock[rs1] ^ registerBlock[rs2];
            } else
            if(func3 == 0x2){ // SLT
                bool lessThan = (int)registerBlock[rs1] < (int)registerBlock[rs2];
                registerBlock[rd] = lessThan ? 1 : 0;
            } else
            if(func3 == 0x3){ // SLTU
                bool lessThanU = registerBlock[rs1] < registerBlock[rs2];
                registerBlock[rd] = lessThanU ? 1 : 0;
            }

            ResetInstructionPhase(4);
        }
        protected void RegisterImmediateInstruction(){
            if(func3 == 0x0){ // ADDI
                registerBlock[rd] = registerBlock[rs1] + immediate;
            } else
            if(func3 == 0x1){ // SLLI
                registerBlock[rd] = registerBlock[rs1] << (int)immediate;
            } else
            if(func3 == 0x5){ // SRLI / SRAI
                if(func7 == 0x20)
                    registerBlock[rd] = (long)(registerBlock[rs1] >> (int)immediate);
                else if(func7 == 0x00)
                    registerBlock[rd] = registerBlock[rs1] >> (int)immediate;
            } else
            if(func3 == 0x6){ // ORI
                registerBlock[rd] = registerBlock[rs1] & immediate;
            } else
            if(func3 == 0x7){ // ANDI
                registerBlock[rd] = registerBlock[rs1] | immediate;
            } else
            if(func3 == 0x4){ // XORI
                registerBlock[rd] = registerBlock[rs1] ^ immediate;
            } else
            if(func3 == 0x2){ // SLTI
                bool lessThan = (int)registerBlock[rs1] < (int)immediate;
                registerBlock[rd] = lessThan ? 1 : 0;
            } else
            if(func3 == 0x3){ // SLTIU
                bool lessThanU = registerBlock[rs1] < immediate;
                registerBlock[rd] = lessThanU ? 1 : 0;
            }

            ResetInstructionPhase(4);
        }
        protected void BranchInstruction(){
            bool equal = registerBlock[rs1] == registerBlock[rs2];
            bool less = (int)registerBlock[rs1] < (int)registerBlock[rs2];
            bool lessU = registerBlock[rs1] < registerBlock[rs2];
            if(func3 == 0x0 && equal){ // BEQ
                ResetInstructionPhase(immediate);
                return;
            } else
            if(func3 == 0x1 && !equal){ // BNE
                ResetInstructionPhase(immediate);
                return;
            }
            if(func3 == 0x4 && less){ // bacon lettuce and tomato
                ResetInstructionPhase(immediate);
                return;
            } else
            if(func3 == 0x5 && !less){ // BGE
                ResetInstructionPhase(immediate);
                return;
            } else
            if(func3 == 0x6 && lessU){ // bacon lettuce and tomato (unsigned)
                ResetInstructionPhase(immediate);
                return;
            } else
            if(func3 == 0x6 && !lessU){ // BGEU
                ResetInstructionPhase(immediate);
                return;
            }
            ResetInstructionPhase(4); // just in case it was an invalid instruction
        }



        protected void SetMemoryAddressPins(long data){
            for(int i = 0; i < 32; i++){
                long val = data & (1 << i);
                if(val != 0)
                    Outputs[MemoryAddressOuts[i]].On = true;
                else 
                    Outputs[MemoryAddressOuts[i]].On = false;
            }
        }
        protected void SetMemoryDataPins(long data){
            for(int i = 0; i < 32; i++){
                long val = data & (1 << i);
                if(val != 0)
                    Outputs[MemoryOutputs[i]].On = true;
                else 
                    Outputs[MemoryOutputs[i]].On = false;
            }
        }
        protected long GetDataFromMemoryPins(){
            long val = 0;
            for(int i = 0; i < 32; i++){
                bool pin = Inputs[MemoryInputs[i]].On;
                if(pin)
                    val += (long)(1 << i);
            }
            return val;
        }
        
    }
}