"""
Order Service Module

This module provides the OrderService class for managing e-commerce orders.
Handles order creation, payment processing, inventory management, and notifications.
"""

from typing import List, Optional
from datetime import datetime


class OrderService:
    """
    Service for managing e-commerce orders and order processing.
    
    This service orchestrates the complete order lifecycle including:
    - Order validation and creation
    - Payment processing
    - Inventory reservation
    - Order fulfillment
    - Customer notifications
    
    Attributes:
        order_repository: Repository for order data persistence
        payment_service: Service for processing payments
        inventory_service: Service for managing product inventory
        notification_service: Service for sending customer notifications
        logger: Logger instance for order operations
    
    Example:
        >>> order_service = OrderService(order_repo, payment_svc, inventory_svc, notif_svc, logger)
        >>> order = order_service.create_order(customer_id=123, items=[...])
        >>> order_service.process_payment(order.id, payment_method="credit_card")
    """
    
    def __init__(self, order_repository, payment_service, inventory_service, 
                 notification_service, logger):
        """
        Initialize the OrderService with required dependencies.
        
        Args:
            order_repository: Repository for order data access
            payment_service: Service for payment processing
            inventory_service: Service for inventory management
            notification_service: Service for notifications
            logger: Logger instance
        """
        self.order_repository = order_repository
        self.payment_service = payment_service
        self.inventory_service = inventory_service
        self.notification_service = notification_service
        self.logger = logger
    
    async def create_order(self, customer_id: int, items: List[dict], 
                          shipping_address: dict) -> dict:
        """
        Create a new order in the system.
        
        Validates order data, checks inventory availability, reserves stock,
        and creates the order record. Does not process payment.
        
        Args:
            customer_id: ID of the customer placing the order
            items: List of order items with product_id and quantity
            shipping_address: Dictionary containing shipping address details
        
        Returns:
            dict: Created order object with order_id, status, and total
        
        Raises:
            ValidationError: If order data is invalid
            InsufficientStockError: If requested items are out of stock
            
        Example:
            >>> items = [{"product_id": 101, "quantity": 2}, {"product_id": 202, "quantity": 1}]
            >>> address = {"street": "123 Main St", "city": "NYC", "zip": "10001"}
            >>> order = await service.create_order(customer_id=456, items=items, shipping_address=address)
        """
        self.logger.info(f"Creating order for customer {customer_id}")
        
        # Validate order items
        if not items:
            raise ValidationError("Order must contain at least one item")
        
        # Check inventory availability
        for item in items:
            available = await self.inventory_service.check_availability(
                item['product_id'], 
                item['quantity']
            )
            if not available:
                raise InsufficientStockError(
                    f"Insufficient stock for product {item['product_id']}"
                )
        
        # Reserve inventory
        reservation_id = await self.inventory_service.reserve_items(items)
        
        # Calculate order total
        total = await self._calculate_total(items)
        
        # Create order record
        order = await self.order_repository.create({
            'customer_id': customer_id,
            'items': items,
            'shipping_address': shipping_address,
            'total': total,
            'status': 'pending',
            'reservation_id': reservation_id,
            'created_at': datetime.utcnow()
        })
        
        self.logger.info(f"Order {order['id']} created successfully")
        
        # Send confirmation email
        await self.notification_service.send_order_confirmation(order)
        
        return order
    
    async def process_payment(self, order_id: int, payment_method: str, 
                             payment_details: dict) -> bool:
        """
        Process payment for an existing order.
        
        Attempts to charge the customer using the specified payment method.
        Updates order status and releases inventory reservation based on result.
        
        Args:
            order_id: ID of the order to process payment for
            payment_method: Payment method (credit_card, paypal, etc.)
            payment_details: Payment details specific to the method
        
        Returns:
            bool: True if payment successful, False otherwise
        
        Raises:
            OrderNotFoundError: If order doesn't exist
            PaymentError: If payment processing fails
        """
        order = await self.order_repository.get_by_id(order_id)
        if not order:
            raise OrderNotFoundError(f"Order {order_id} not found")
        
        # Attempt payment
        payment_result = await self.payment_service.charge(
            amount=order['total'],
            method=payment_method,
            details=payment_details
        )
        
        if payment_result.success:
            # Update order status
            await self.order_repository.update(order_id, {
                'status': 'paid',
                'payment_id': payment_result.transaction_id,
                'paid_at': datetime.utcnow()
            })
            
            # Confirm inventory reservation
            await self.inventory_service.confirm_reservation(order['reservation_id'])
            
            # Send payment confirmation
            await self.notification_service.send_payment_confirmation(order)
            
            self.logger.info(f"Payment processed successfully for order {order_id}")
            return True
        else:
            # Release inventory reservation
            await self.inventory_service.release_reservation(order['reservation_id'])
            
            self.logger.error(f"Payment failed for order {order_id}: {payment_result.error}")
            return False
    
    async def _calculate_total(self, items: List[dict]) -> float:
        """Calculate the total price for order items including tax and shipping."""
        subtotal = sum(item['price'] * item['quantity'] for item in items)
        tax = subtotal * 0.08  # 8% tax
        shipping = 10.00 if subtotal < 50 else 0  # Free shipping over $50
        return subtotal + tax + shipping

