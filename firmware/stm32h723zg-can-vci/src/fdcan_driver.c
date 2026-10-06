#include "fdcan_driver.h"
#include <string.h>

#ifdef STM32H723xx
#include "stm32h7xx_hal.h"
#else
// Mock definitions for cross-compilation verification if HAL is not present
#ifndef FDCAN1
typedef struct {
    uint32_t placeholder;
} FDCAN_HandleTypeDef;
#endif
#endif

// Ring buffer allocated in DTCM zero-wait-state RAM when compiled on ARM
#if defined(__GNUC__) && defined(STM32H723xx)
__attribute__((section(".dtcmram")))
#endif
static CanMessage s_rx_ring[FDCAN_RX_RING_BUFFER_SIZE];
static volatile uint32_t s_rx_head = 0;
static volatile uint32_t s_rx_tail = 0;

static FdcanStats s_stats = {0};

#ifdef STM32H723xx
static FDCAN_HandleTypeDef hfdcan1;

// Clock Configuration: PLL2 generates 80 MHz FDCAN Kernel Clock
static void FDCAN1_Clock_Config(void) {
    RCC_PeriphCLKInitTypeDef PeriphClkInitStruct = {0};
    PeriphClkInitStruct.PeriphClockSelection = RCC_PERIPHCLK_FDCAN;
    PeriphClkInitStruct.FdcanClockSelection = RCC_FDCANCLKSOURCE_PLL2;
    PeriphClkInitStruct.PLL2.PLL2M = 5;
    PeriphClkInitStruct.PLL2.PLL2N = 80;
    PeriphClkInitStruct.PLL2.PLL2P = 5; // 400 MHz / 5 = 80.0 MHz
    PeriphClkInitStruct.PLL2.PLL2Q = 2;
    PeriphClkInitStruct.PLL2.PLL2R = 2;
    PeriphClkInitStruct.PLL2.PLL2RGE = RCC_PLL2VCIRANGE_2;
    PeriphClkInitStruct.PLL2.PLL2VCOSEL = RCC_PLL2VCOWIDE;
    PeriphClkInitStruct.PLL2.PLL2FRACN = 0;
    HAL_RCCEx_PeriphCLKConfig(&PeriphClkInitStruct);
}

// GPIO Configuration: PD0 -> FDCAN1_RX (CN11 Pin 57), PD1 -> FDCAN1_TX (CN11 Pin 55)
// UM2407 VERIFIED: Do NOT use Arduino D12/D13!
static void FDCAN1_GPIO_Config(void) {
    __HAL_RCC_GPIOD_CLK_ENABLE();
    GPIO_InitTypeDef GPIO_InitStruct = {0};
    GPIO_InitStruct.Pin = GPIO_PIN_0 | GPIO_PIN_1;
    GPIO_InitStruct.Mode = GPIO_MODE_AF_PP;
    GPIO_InitStruct.Pull = GPIO_NOPULL;
    GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_VERY_HIGH;
    GPIO_InitStruct.Alternate = GPIO_AF9_FDCAN1;
    HAL_GPIO_Init(GPIOD, &GPIO_InitStruct);
}
#endif

bool FDCAN1_Driver_Init(FdcanOperatingMode mode) {
    s_stats.current_mode = mode;
    s_stats.is_listen_only = (mode == FDCAN_OPMODE_LISTEN_ONLY);

#ifdef STM32H723xx
    FDCAN1_Clock_Config();
    FDCAN1_GPIO_Config();
    __HAL_RCC_FDCAN_CLK_ENABLE();

    hfdcan1.Instance = FDCAN1;
    hfdcan1.Init.FrameFormat = FDCAN_FRAME_FD_BRS;

    // Operating mode selection
    switch (mode) {
        case FDCAN_OPMODE_LISTEN_ONLY:
            hfdcan1.Init.Mode = FDCAN_MODE_BUS_MONITORING; // Hardware Listen-Only (CCCR.MON=1)
            break;
        case FDCAN_OPMODE_INTERNAL_LOOPBACK:
            hfdcan1.Init.Mode = FDCAN_MODE_INTERNAL_LOOPBACK; // Test 2: Silicon self-test
            break;
        case FDCAN_OPMODE_EXTERNAL_LOOPBACK:
            hfdcan1.Init.Mode = FDCAN_MODE_EXTERNAL_LOOPBACK;
            break;
        case FDCAN_OPMODE_NORMAL:
        default:
            hfdcan1.Init.Mode = FDCAN_MODE_NORMAL;
            break;
    }

    hfdcan1.Init.AutoRetransmission = (mode == FDCAN_OPMODE_LISTEN_ONLY) ? DISABLE : ENABLE;
    hfdcan1.Init.TransmitPause = DISABLE;
    hfdcan1.Init.ProtocolException = DISABLE;

    // Nominal Timing: 500 kbps @ 80 MHz kernel (160 tq, 80% sample point)
    hfdcan1.Init.NominalPrescaler = 1;
    hfdcan1.Init.NominalSyncJumpWidth = 32;
    hfdcan1.Init.NominalTimeSeg1 = 127;
    hfdcan1.Init.NominalTimeSeg2 = 32;

    // Data Timing: 2 Mbps @ 80 MHz kernel (40 tq, 80% sample point)
    hfdcan1.Init.DataPrescaler = 1;
    hfdcan1.Init.DataSyncJumpWidth = 8;
    hfdcan1.Init.DataTimeSeg1 = 31;
    hfdcan1.Init.DataTimeSeg2 = 8;

    // Message RAM Layout (10 KB shared SRAM)
    hfdcan1.Init.MessageRAMOffset = 0;
    hfdcan1.Init.StdFiltersNbr = 1;
    hfdcan1.Init.ExtFiltersNbr = 1;
    hfdcan1.Init.RxFifo0ElmtsNbr = 64;
    hfdcan1.Init.RxFifo0ElmtSize = FDCAN_DATA_BYTES_64;
    hfdcan1.Init.RxFifo1ElmtsNbr = 0;
    hfdcan1.Init.RxBuffersNbr = 0;
    hfdcan1.Init.TxEventsNbr = 16;
    hfdcan1.Init.TxBuffersNbr = 0;
    hfdcan1.Init.TxFifoQueueElmtsNbr = 16;
    hfdcan1.Init.TxFifoQueueMode = FDCAN_TX_FIFO_OPERATION;
    hfdcan1.Init.TxElmtSize = FDCAN_DATA_BYTES_64;

    if (HAL_FDCAN_Init(&hfdcan1) != HAL_OK) {
        return false;
    }

    // Promiscuous Acceptance Filter: Accept all frames into RX FIFO 0
    FDCAN_FilterTypeDef sFilterConfig = {0};
    sFilterConfig.IdType = FDCAN_STANDARD_ID;
    sFilterConfig.FilterIndex = 0;
    sFilterConfig.FilterType = FDCAN_FILTER_MASK;
    sFilterConfig.FilterConfig = FDCAN_FILTER_TO_RXFIFO0;
    sFilterConfig.FilterID1 = 0x000;
    sFilterConfig.FilterID2 = 0x000;
    HAL_FDCAN_ConfigFilter(&hfdcan1, &sFilterConfig);

    HAL_FDCAN_ConfigGlobalFilter(&hfdcan1, FDCAN_ACCEPT_IN_RX_FIFO0, FDCAN_ACCEPT_IN_RX_FIFO0,
                                 FDCAN_FILTER_REMOTE, FDCAN_FILTER_REMOTE);

    // Activate FIFO 0 New Message Interrupt
    HAL_FDCAN_ActivateNotification(&hfdcan1, FDCAN_IT_RX_FIFO0_NEW_MESSAGE, 0);

    // Configure NVIC for FDCAN1 Interrupt (Priority 1)
    HAL_NVIC_SetPriority(FDCAN1_IT0_IRQn, 1, 0);
    HAL_NVIC_EnableIRQ(FDCAN1_IT0_IRQn);

    // Start Controller
    if (HAL_FDCAN_Start(&hfdcan1) != HAL_OK) {
        return false;
    }
#endif

    s_rx_head = 0;
    s_rx_tail = 0;
    return true;
}

#ifdef STM32H723xx
// High-priority ISR: Direct Message RAM extraction
void HAL_FDCAN_RxFifo0Callback(FDCAN_HandleTypeDef *hfdcan, uint32_t RxFifo0ITs) {
    if ((RxFifo0ITs & FDCAN_IT_RX_FIFO0_NEW_MESSAGE) != 0) {
        FDCAN_RxHeaderTypeDef rxHeader;
        uint8_t rxData[64];

        if (HAL_FDCAN_GetRxMessage(hfdcan, FDCAN_RX_FIFO0, &rxHeader, rxData) == HAL_OK) {
            uint32_t next_head = (s_rx_head + 1) % FDCAN_RX_RING_BUFFER_SIZE;
            if (next_head != s_rx_tail) {
                CanMessage* slot = &s_rx_ring[s_rx_head];
                slot->id = rxHeader.Identifier;
                slot->flags = 0;
                if (rxHeader.IdType == FDCAN_EXTENDED_ID) {
                    slot->flags |= CAN_FLAG_EXTENDED;
                }
                if (rxHeader.RxFrameType == FDCAN_REMOTE_FRAME) {
                    slot->flags |= CAN_FLAG_RTR;
                }
                if (rxHeader.FDFormat == FDCAN_FD_CAN) {
                    slot->flags |= CAN_FLAG_FD;
                }
                if (rxHeader.BitRateSwitch == FDCAN_BRS_ON) {
                    slot->flags |= CAN_FLAG_BRS;
                }

                // Convert FDCAN DLC code to byte length
                static const uint8_t dlc_lut[] = {0, 1, 2, 3, 4, 5, 6, 7, 8, 12, 16, 20, 24, 32, 48, 64};
                uint8_t raw_dlc = (rxHeader.DataLength >> 16) & 0x0F;
                slot->dlc = (raw_dlc < sizeof(dlc_lut)) ? dlc_lut[raw_dlc] : 8;

                memcpy(slot->data, rxData, slot->dlc);
                slot->timestamp_us = HAL_GetTick() * 1000; // Microsecond resolution
                slot->channel = 0; // FDCAN1

                s_rx_head = next_head;
                s_stats.rx_frames++;
            } else {
                s_stats.dropped_frames++;
            }
        }
    }
}
#endif

bool FDCAN1_Driver_ReadFrame(CanMessage* out_msg) {
    if (!out_msg) return false;
    if (s_rx_head == s_rx_tail) {
        return false; // Ring buffer empty
    }

    *out_msg = s_rx_ring[s_rx_tail];
    s_rx_tail = (s_rx_tail + 1) % FDCAN_RX_RING_BUFFER_SIZE;
    return true;
}

bool FDCAN1_Driver_Transmit(const CanMessage* msg) {
    if (!msg) return false;

    // Safety Gate: Transmit is strictly prohibited in Listen-Only mode
    if (s_stats.is_listen_only) {
        return false;
    }

#ifdef STM32H723xx
    FDCAN_TxHeaderTypeDef txHeader = {0};
    txHeader.Identifier = msg->id;
    txHeader.IdType = (msg->flags & CAN_FLAG_EXTENDED) ? FDCAN_EXTENDED_ID : FDCAN_STANDARD_ID;
    txHeader.TxFrameType = (msg->flags & CAN_FLAG_RTR) ? FDCAN_REMOTE_FRAME : FDCAN_DATA_FRAME;
    txHeader.FDFormat = (msg->flags & CAN_FLAG_FD) ? FDCAN_FD_CAN : FDCAN_CLASSIC_CAN;
    txHeader.BitRateSwitch = (msg->flags & CAN_FLAG_BRS) ? FDCAN_BRS_ON : FDCAN_BRS_OFF;
    txHeader.MessageMarker = 0;

    // Convert length to FDCAN DLC code
    if (msg->dlc <= 8) txHeader.DataLength = msg->dlc << 16;
    else if (msg->dlc <= 12) txHeader.DataLength = FDCAN_DLC_BYTES_12;
    else if (msg->dlc <= 16) txHeader.DataLength = FDCAN_DLC_BYTES_16;
    else if (msg->dlc <= 20) txHeader.DataLength = FDCAN_DLC_BYTES_20;
    else if (msg->dlc <= 24) txHeader.DataLength = FDCAN_DLC_BYTES_24;
    else if (msg->dlc <= 32) txHeader.DataLength = FDCAN_DLC_BYTES_32;
    else if (msg->dlc <= 48) txHeader.DataLength = FDCAN_DLC_BYTES_48;
    else txHeader.DataLength = FDCAN_DLC_BYTES_64;

    if (HAL_FDCAN_AddMessageToTxFifoQ(&hfdcan1, &txHeader, (uint8_t*)msg->data) == HAL_OK) {
        s_stats.tx_frames++;
        return true;
    }
#endif
    return false;
}

void FDCAN1_Driver_GetStats(FdcanStats* out_stats) {
    if (out_stats) {
        *out_stats = s_stats;
    }
}
