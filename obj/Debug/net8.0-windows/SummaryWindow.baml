   M S B A M L   `   `   `  ����  PhotoPrismCleanup%    PhotoPrismCleanup.SummaryWindow   5   	   W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	     � Title$  Confirm Deletion��5   	    � SizeToContent$ WidthAndHeight=�5   	    � WindowStartupLocation$ CenterOwner=�5   	      WindowBackground#���   5   	     ControlForeground#���  5   	    � 	Resources 5	        	BoolToVis  MediaItemTemplate  ControlBackground �� 	Converter �� ConverterParameter��%b  &	       &	      7  7  �� 5
   
   6'   �� 5   
   .R�5      �� $	��100��6   $��5q�6%   .+�5      �� #���  6   $	��100��5      $	��100��6   ._�5      � .+�5      �� $��Uniform=�5      ��5       �� *	Thumbnail+y�5      �� *	IsVideo+8   $ False���� 5      $	��▶��6$   $��24(�5      
�������$��Center=�5      $��Center=�5      y�5      �� *	IsVideo+8  5      5      �� 5      $��CharacterEllipsis=�5      $��Center=�5      $��0,4,0,0q�5      ��5      �� *
FileName+5      5      5       5!      .��5#      �� $��10q�6   .+�5$   
   �� -   5         ��Name$	 Lbl��5$      $
��Bold"�6!   $��0,0,0,10q�63   �� 5%   
   -   5         ��Name$ DownloadBefore��5%      $"��Download all before deleting��5&      $��0,0,0,10q�5'      �� 5(   
   $	��200��6   .��5)      �� -   5         ��Name$ 	ThumbList��5)      #��[ 5*      ��5+      �� 5,      .R�5-      $� 	 $� Orientation$	 
Horizontal=�61   5.      50      51      � 52   
   -   5        
 �Name$
 DelProgressBar��52      $��8��53       � Minimum$ 0Q�54       � Maximum$ 1Q�55       � Value$ 0Q�56      $y�	Collapsed=�57      $��0,10,0,0q�58      � 59   
   $��Right=�6   $��0,10,0,0q�60   .+�5:      �� -   5         ��Name$ OK��5:      $��Delete Items��6!   $	��120��68   $��0,0,8,0q�5;       �� 5<      -   5        $ Cancel��5<      $��Cancel��6%   $��80��66   5=      5>      �� 5?   
   -   5        $ 
ResultText��5?      $y�	Collapsed=�5@      $��0,10,0,0q�5A      $
��Wrap=�5B      �� 5C   
   -   5        $ CloseBtn��5C      $��Close��6#   $y�	Collapsed=�5D      $��Right=�5E      $��80��5F      $��0,8,0,0q�5H      5I      5J      