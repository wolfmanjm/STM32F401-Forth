\ mcp23017 driver

#require i2c.fs

$20 constant MCP23017_I2C_ADDRESS

\ registers
$00 constant MCP23017_IODIR_A   \  Controls the direction of the data I/O for port A.
$01 constant MCP23017_IODIR_B   \  Controls the direction of the data I/O for port B.
$02 constant MCP23017_IPOL_A    \  Configures the polarity on the corresponding GPIO_ port bits for port A.
$03 constant MCP23017_IPOL_B    \  Configures the polarity on the corresponding GPIO_ port bits for port B.
$04 constant MCP23017_GPINTEN_A \  Controls the interrupt-on-change for each pin of port A.
$05 constant MCP23017_GPINTEN_B \  Controls the interrupt-on-change for each pin of port B.
$06 constant MCP23017_DEFVAL_A  \  Controls the default comparison value for interrupt-on-change for port A.
$07 constant MCP23017_DEFVAL_B  \  Controls the default comparison value for interrupt-on-change for port B.
$08 constant MCP23017_INTCON_A  \  Controls how the associated pin value is compared for the interrupt-on-change for port A.
$09 constant MCP23017_INTCON_B  \  Controls how the associated pin value is compared for the interrupt-on-change for port B.
$0A constant MCP23017_IOCON     \  Controls the device.
$0C constant MCP23017_GPPU_A    \  Controls the pull-up resistors for the port A pins.
$0D constant MCP23017_GPPU_B    \  Controls the pull-up resistors for the port B pins.
$0E constant MCP23017_INTF_A    \  Reflects the interrupt condition on the port A pins.
$0F constant MCP23017_INTF_B    \  Reflects the interrupt condition on the port B pins.
$10 constant MCP23017_INTCAP_A  \  Captures the port A value at the time the interrupt occured.
$11 constant MCP23017_INTCAP_B  \  Captures the port B value at the time the interrupt occured.
$12 constant MCP23017_GPIO_A    \  Reflects the value on the port A.
$13 constant MCP23017_GPIO_B    \  Reflects the value on the port B.
$14 constant MCP23017_OLAT_A    \  Provides access to the port A output latches.
$15 constant MCP23017_OLAT_B    \  Provides access to the port B output latches.

: mcp23017-set-reg ( val reg -- )
    MCP23017_I2C_ADDRESS i2c-send2stop if ." setreg failed" exit then
;

\ if bit 15 is set it is an error
: mcp23017-get-reg ( reg -- val )
    MCP23017_I2C_ADDRESS i2c-getreg
;

: mcp23017-init
    \ check it is there
    MCP23017_I2C_ADDRESS i2c-deviceready? not if ." MCP23017 not found at address: " MCP23017_I2C_ADDRESS hex. exit then

    \ BANK =    0 : sequential register addresses
    \ MIRROR =  0 : use configureInterrupt
    \ SEQOP =   1 : sequential operation disabled, address pointer does not increment
    \ DISSLW =  0 : slew rate enabled
    \ HAEN =    0 : hardware address pin is always enabled on 23017
    \ ODR =     0 : active driver output (INTPOL bit sets the polarity.)

    \ INTPOL =  0 : interrupt active low
    \ UNIMPLMENTED  0 : unimplemented: Read as ‘0’

    %00100000 MCP23017_IOCON mcp23017-set-reg
;

: bitset ( x bit -- y )
    1 swap lshift or
;

: bitclr ( x bit -- y )
    1 swap lshift bic
;

: bittst ( x bit -- flg )
    1 swap lshift and if true else false then
;

\ set pin (0..7) on port (0..1) to an output
: mcp23017-output ( pin port -- )
    0= if MCP23017_IODIR_A else MCP23017_IODIR_B then dup >r
    mcp23017-get-reg        \ current contents
    swap bitclr
    r> mcp23017-set-reg
;

\ set pin (0..7) on port (0..1) to an input
: mcp23017-input ( pin port -- )
    0= if MCP23017_IODIR_A else MCP23017_IODIR_B then dup >r
    mcp23017-get-reg        \ current contents
    swap bitset
    r> mcp23017-set-reg
;

\ set pin (0..7) on port (0..1) as an input with pullup
: mcp23017-inputpu ( pin port -- )
    2dup mcp23017-input
    0= if MCP23017_GPPU_A else MCP23017_GPPU_B then dup >r
    mcp23017-get-reg        \ current contents
    swap bitset
    r> mcp23017-set-reg
;


\ set pin (0..7) on port (0..1) as inverted
: mcp23017-inverted ( pin port -- )
    0= if MCP23017_IPOL_A else MCP23017_IPOL_B then dup >r
    mcp23017-get-reg        \ current contents
    swap bitset
    r> mcp23017-set-reg
;

\ set or clear pin (0..7) on port (0..1)
: mcp23017-setpin ( val pin port -- )
    0= if MCP23017_GPIO_A else MCP23017_GPIO_B then dup >r
    mcp23017-get-reg                    \ current contents
    swap rot if bitset else bitclr then
    r> mcp23017-set-reg
;

\ get value of pin (0..7) on port (0..1)
: mcp23017-getpin ( pin port -- flg )
    0= if MCP23017_GPIO_A else MCP23017_GPIO_B then
    mcp23017-get-reg                    \ current contents
    swap bittst
;

: mcp23017-dump-regs
    mcp23017-init

    cr
    MCP23017_IODIR_A mcp23017-get-reg dup $8000 and if drop ." got rx error" exit then ." IODIR_A : "  hex. cr
    MCP23017_IODIR_B mcp23017-get-reg ." IODIR_B : "  hex. cr
    MCP23017_IPOL_A mcp23017-get-reg ." IPOL_A : "  hex. cr
    MCP23017_IPOL_B mcp23017-get-reg ." IPOL_B : "  hex. cr
    MCP23017_GPINTEN_A mcp23017-get-reg ." GPINTEN_A : "  hex. cr
    MCP23017_GPINTEN_B mcp23017-get-reg ." GPINTEN_B : "  hex. cr
    MCP23017_DEFVAL_A mcp23017-get-reg ." DEFVAL_A : "  hex. cr
    MCP23017_DEFVAL_B mcp23017-get-reg ." DEFVAL_B : "  hex. cr
    MCP23017_INTCON_A mcp23017-get-reg ." INTCON_A : "  hex. cr
    MCP23017_INTCON_B mcp23017-get-reg ." INTCON_B : "  hex. cr
    MCP23017_IOCON mcp23017-get-reg ." IOCON : "  hex. cr
    MCP23017_GPPU_A mcp23017-get-reg ." GPPU_A : "  hex. cr
    MCP23017_GPPU_B mcp23017-get-reg ." GPPU_B : "  hex. cr
    MCP23017_INTF_A mcp23017-get-reg ." INTF_A : "  hex. cr
    MCP23017_INTF_B mcp23017-get-reg ." INTF_B : "  hex. cr
    MCP23017_INTCAP_A mcp23017-get-reg ." INTCAP_A : "  hex. cr
    MCP23017_INTCAP_B mcp23017-get-reg ." INTCAP_B : "  hex. cr
    MCP23017_GPIO_A mcp23017-get-reg ." GPIO_A : "  hex. cr
    MCP23017_GPIO_B mcp23017-get-reg ." GPIO_B : "  hex. cr
    MCP23017_OLAT_A mcp23017-get-reg ." OLAT_A : "  hex. cr
    MCP23017_OLAT_B mcp23017-get-reg ." OLAT_B : "  hex. cr
;

: mcp-test
    i2c-init
    mcp23017-init
    0 0 mcp23017-output  \ set A0 to output
    7 1 mcp23017-output  \ set B7 to output
    0 1 mcp23017-inputpu \ set B0 to input pullup

    0 0 0 mcp23017-setpin \ set A0 low
    1 7 1 mcp23017-setpin \ set B7 high

    0
    begin
        0= if 1 else 0 then
        dup
        0 0 mcp23017-setpin         \ toggle A0
        0 1 mcp23017-getpin if ." set" else ." clr" then cr \ print out state of B0
        1000 ms
    key? until
    drop
;
