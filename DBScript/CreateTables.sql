USE [DEV_Orders]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [tblOrder](
	[orderID] [int] IDENTITY(1,1) NOT NULL,
	[OrderReference] [nvarchar](50) NOT NULL,
	[OrderMarketplace] [nvarchar](50) NOT NULL,
	[OrderFirstname] [nvarchar](50) NOT NULL,
	[OrderSurname] [nvarchar](50) NOT NULL,
	[Processed] [bit] NOT NULL CONSTRAINT [DF_tblOrder_Processed]  DEFAULT ((0)),
 CONSTRAINT [PK_tblOrder] PRIMARY KEY CLUSTERED 
(
	[OrderReference] ASC,
	[OrderMarketplace] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

CREATE TABLE [tblOrderItem](
	[orderItemID] [int] IDENTITY(1,1) NOT NULL,
	[OrderReference] [nvarchar](50) NOT NULL,
	[OrderMarketplace] [nvarchar](50) NOT NULL,
	[OrderItemNumber] [int] NOT NULL,
	[SKU] [nvarchar](50) NOT NULL,
	[PricePerUnit] [decimal](18, 0) NOT NULL,
	[Quantity] [int] NOT NULL,
 CONSTRAINT [PK_tblOrderItem] PRIMARY KEY CLUSTERED 
(
	[OrderReference] ASC,
	[OrderMarketplace] ASC,
	[OrderItemNumber] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

CREATE TABLE [tblShipment](
	[shipmentID] [int] IDENTITY(1,1) NOT NULL,
	[OrderReference] [nvarchar](50) NOT NULL,
	[OrderMarketplace] [nvarchar](50) NOT NULL,
	[ShippingService] [nvarchar](50) NOT NULL,
	[Postcode] [nvarchar](10) NOT NULL,
 CONSTRAINT [PK_tblShipment] PRIMARY KEY CLUSTERED 
(
	[OrderReference] ASC,
	[OrderMarketplace] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

