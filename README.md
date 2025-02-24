This is a collection of drivers I have written for the STM32F401 version of mecrisp forth RA version.

Most of these I have written by following the HAL drivers, and some have taken parts from other mecrisp forth contributions.

License for my original work is BSD 2-clause,
other code is licensed by the original authors found in the mecrisp stellaris repo.

Most of these require the slightly modified versions of lib_registers.txt and lib_systick.txt to be loaded first.


* 84MHz.fs sets the clock the 84MHz and sets HCLK accordingly, also sets the baud rate to match.
* i2c.fs implements H/W I2C based on the HAL drivers
* spi1.fs sets up SPI1 again mostly based on the HAL drivers
* qei.fs sets up a timer to handle a quadrature encoder in H/W
* 24lc256.fs is a driver for the eeprom chip using the I2C protocol
* psram.fs driver for the standard 8MB psram chip communications over SPI
* gpio-simple.fs is a simplified way to set GPIO pins for input or output
* mcp23017.fs is a driver for the I2C based GPIO expander chip
* mpu6050.fs is a driver for the IMU chip using I2C communications
* ssd1306.fs and glcdfont.fs is a simple driver for the LCD using I2C for communication mostly based on other community supplied drivers


The reaction wheel project is based on:

One axis self balancing stick (DC motor) by remrc on Thingiverse: https://www.thingiverse.com/thing:5569612

Ported from the arduino c version.
